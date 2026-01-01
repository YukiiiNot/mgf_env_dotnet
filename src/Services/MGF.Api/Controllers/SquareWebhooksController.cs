namespace MGF.Api.Controllers;

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using MGF.Api.Square;
using MGF.Domain.Entities;
using MGF.Data.Data;
using MGF.Data.Abstractions;

[ApiController]
[Route("webhooks/square")]
public sealed class SquareWebhooksController : ControllerBase
{
    private const int MaxWebhookBodyBytes = 262_144;
    private const string SignatureHeaderName = "x-square-hmacsha256-signature";
    private const string LegacySignatureHeaderName = "x-square-signature";

    private readonly AppDbContext db;
    private readonly ISquareWebhookStore webhookStore;
    private readonly IConfiguration configuration;
    private readonly ISquareWebhookVerifier verifier;
    private readonly ILogger<SquareWebhooksController> logger;

    public SquareWebhooksController(
        AppDbContext db,
        ISquareWebhookStore webhookStore,
        IConfiguration configuration,
        ISquareWebhookVerifier verifier,
        ILogger<SquareWebhooksController> logger
    )
    {
        this.db = db;
        this.webhookStore = webhookStore;
        this.configuration = configuration;
        this.verifier = verifier;
        this.logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(MaxWebhookBodyBytes)]
    public async Task<IActionResult> ReceiveAsync(CancellationToken cancellationToken)
    {
        var signatureKey = configuration["Square:WebhookSignatureKey"];
        if (string.IsNullOrWhiteSpace(signatureKey))
        {
            logger.LogError("MGF.Api: missing config value Square:WebhookSignatureKey; rejecting Square webhook.");
            return Unauthorized();
        }

        var notificationUrl =
            configuration["Square:WebhookNotificationUrl"] ?? configuration["Integrations:Square:WebhookNotificationUrl"];
        if (string.IsNullOrWhiteSpace(notificationUrl))
        {
            logger.LogError(
                "MGF.Api: missing config value Square:WebhookNotificationUrl (or Integrations:Square:WebhookNotificationUrl); cannot verify Square webhook signature."
            );
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (Request.ContentLength is > MaxWebhookBodyBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        byte[] bodyBytes;
        try
        {
            bodyBytes = await ReadBodyBytesAsync(Request, MaxWebhookBodyBytes, cancellationToken);
        }
        catch (PayloadTooLargeException)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (bodyBytes.Length == 0)
        {
            return BadRequest();
        }

        var signatureHeaderPresent =
            Request.Headers.ContainsKey(SignatureHeaderName) || Request.Headers.ContainsKey(LegacySignatureHeaderName);

        var providedSignature = GetSignatureHeader(Request.Headers);
        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            LogSignatureFailure(signatureHeaderPresent, bodyBytes);
            return Unauthorized();
        }

        if (!verifier.IsValid(notificationUrl, bodyBytes, signatureKey, providedSignature))
        {
            LogSignatureFailure(signatureHeaderPresent, bodyBytes);
            return Unauthorized();
        }

        JsonDocument payloadDoc;
        try
        {
            payloadDoc = JsonDocument.Parse(bodyBytes);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "MGF.Api: Square webhook payload is not valid JSON.");
            return BadRequest();
        }

        using (payloadDoc)
        {
            var root = payloadDoc.RootElement;

            var squareEventId = GetString(root, ["event_id", "eventId"]);
            if (string.IsNullOrWhiteSpace(squareEventId))
            {
                logger.LogWarning("MGF.Api: Square webhook missing event_id; cannot persist.");
                return BadRequest();
            }

            var eventType = GetString(root, ["type", "event_type", "eventType"]) ?? "unknown";

            var locationId = GetString(root, ["location_id", "locationId"]);

            string? objectType = null;
            string? objectId = null;

            if (TryGetObject(root, "data", out var data))
            {
                if (TryGetObject(data, "object", out var obj))
                {
                    objectType = GetString(obj, ["type", "object_type", "objectType"]);
                    objectId = GetString(obj, ["id", "object_id", "objectId"]);
                    locationId ??= GetString(obj, ["location_id", "locationId"]);
                }

                objectType ??= GetString(data, ["type", "object_type", "objectType"]);
                objectId ??= GetString(data, ["id", "object_id", "objectId"]);
                locationId ??= GetString(data, ["location_id", "locationId"]);
            }

            var payloadJson = Encoding.UTF8.GetString(bodyBytes);

            var insertedEventCount = 0;

            await db.Database.OpenConnectionAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                insertedEventCount = await webhookStore.InsertEventAsync(
                    new SquareWebhookEventRecord(
                        squareEventId,
                        eventType,
                        objectType,
                        objectId,
                        locationId,
                        payloadJson),
                    cancellationToken);

                if (insertedEventCount > 0)
                {
                    var jobId = EntityIds.NewWithPrefix("job");
                    var jobPayloadJson = JsonSerializer.Serialize(new { square_event_id = squareEventId });

                    await webhookStore.EnqueueProcessingJobAsync(jobId, jobPayloadJson, squareEventId, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }

            if (insertedEventCount == 0)
            {
                logger.LogInformation(
                    "MGF.Api: Square webhook duplicate ignored (event_id={SquareEventId}, type={EventType})",
                    squareEventId,
                    eventType
                );
            }
            else
            {
                logger.LogInformation(
                    "MGF.Api: Square webhook stored and enqueued (event_id={SquareEventId}, type={EventType})",
                    squareEventId,
                    eventType
                );
            }

            return Ok();
        }
    }

    private static string? GetSignatureHeader(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(SignatureHeaderName, out StringValues signature) && !StringValues.IsNullOrEmpty(signature))
        {
            return signature.ToString();
        }

        if (
            headers.TryGetValue(LegacySignatureHeaderName, out StringValues legacy)
            && !StringValues.IsNullOrEmpty(legacy)
        )
        {
            return legacy.ToString();
        }

        return null;
    }

    private static async Task<byte[]> ReadBodyBytesAsync(
        HttpRequest request,
        int maxBytes,
        CancellationToken cancellationToken
    )
    {
        await using var ms = new MemoryStream();

        var buffer = new byte[16 * 1024];
        var totalBytes = 0;

        int bytesRead;
        while ((bytesRead = await request.Body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > maxBytes)
            {
                throw new PayloadTooLargeException();
            }

            await ms.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return ms.ToArray();
    }

    private void LogSignatureFailure(bool signatureHeaderPresent, byte[] bodyBytes)
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        string? squareEventId = null;
        string? eventType = null;

        try
        {
            using var doc = JsonDocument.Parse(bodyBytes);
            var root = doc.RootElement;
            squareEventId = GetString(root, ["event_id", "eventId"]);
            eventType = GetString(root, ["type", "event_type", "eventType"]);
        }
        catch
        {
            // best-effort only; do not fail the request due to logging
        }

        logger.LogWarning(
            "MGF.Api: rejecting Square webhook (remote_ip={RemoteIp}, signature_header_present={SignatureHeaderPresent}, event_id={SquareEventId}, event_type={EventType})",
            remoteIp,
            signatureHeaderPresent,
            squareEventId,
            eventType
        );
    }

    private sealed class PayloadTooLargeException : Exception { }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement obj)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                break;
            }

            obj = property.Value;
            return true;
        }

        obj = default;
        return false;
    }

    private static string? GetString(JsonElement element, IReadOnlyList<string> propertyNames)
    {
        foreach (var name in propertyNames)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                if (property.Value.ValueKind == JsonValueKind.Number)
                {
                    return property.Value.GetRawText();
                }
            }
        }

        return null;
    }
}

