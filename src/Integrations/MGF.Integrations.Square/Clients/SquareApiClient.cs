namespace MGF.Integrations.Square;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public sealed class SquareApiClient
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<SquareApiClient> logger;

    public SquareApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<SquareApiClient> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<SquarePaymentsPage> ListPaymentsAsync(
        string accessToken,
        string locationId,
        DateTimeOffset beginTimeUtc,
        string? cursor,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var baseUrl = GetBaseUrl();

        var url =
            $"{baseUrl}/v2/payments?location_id={Uri.EscapeDataString(locationId)}"
            + $"&begin_time={Uri.EscapeDataString(beginTimeUtc.ToString("O", CultureInfo.InvariantCulture))}"
            + $"&sort_order=ASC&limit={limit}";

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        using var response = await SendWithRetryAsync(
            requestFactory: () => CreateRequest(HttpMethod.Get, url, accessToken),
            cancellationToken: cancellationToken
        );

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);

        var payments = new List<SquarePaymentListItem>();
        var root = doc.RootElement;

        if (TryGetArray(root, "payments", out var paymentsArray))
        {
            foreach (var paymentElement in paymentsArray.EnumerateArray())
            {
                var paymentId = GetString(paymentElement, "id");
                if (string.IsNullOrWhiteSpace(paymentId))
                {
                    continue;
                }

                var updatedAt =
                    ParseUtcTimestamp(GetString(paymentElement, "updated_at"))
                    ?? ParseUtcTimestamp(GetString(paymentElement, "created_at"));

                payments.Add(new SquarePaymentListItem(paymentId, updatedAt));
            }
        }

        var nextCursor = GetString(root, "cursor");

        return new SquarePaymentsPage(payments, nextCursor);
    }

    public async Task<SquarePaymentDetail> GetPaymentAsync(
        string accessToken,
        string squarePaymentId,
        CancellationToken cancellationToken
    )
    {
        var baseUrl = GetBaseUrl();

        var url = $"{baseUrl}/v2/payments/{Uri.EscapeDataString(squarePaymentId)}";

        using var response = await SendWithRetryAsync(
            requestFactory: () => CreateRequest(HttpMethod.Get, url, accessToken),
            cancellationToken: cancellationToken
        );

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        if (!TryGetObject(root, "payment", out var payment))
        {
            throw new InvalidOperationException($"Square API response did not contain a payment object (payment_id={squarePaymentId}).");
        }

        var paymentId = GetString(payment, "id") ?? squarePaymentId;
        var customerId = GetString(payment, "customer_id");
        var locationId = GetString(payment, "location_id");

        if (!TryGetObject(payment, "amount_money", out var amountMoney))
        {
            throw new InvalidOperationException($"Square payment missing amount_money (payment_id={paymentId}).");
        }

        var amountCents = GetLong(amountMoney, "amount");
        var currencyCode = GetString(amountMoney, "currency") ?? "USD";

        if (amountCents is null)
        {
            throw new InvalidOperationException($"Square payment amount_money.amount missing/invalid (payment_id={paymentId}).");
        }

        var status = GetString(payment, "status") ?? "UNKNOWN";

        var createdAt = ParseUtcTimestamp(GetString(payment, "created_at"));
        var updatedAt = ParseUtcTimestamp(GetString(payment, "updated_at"));

        return new SquarePaymentDetail(
            PaymentId: paymentId,
            CustomerId: customerId,
            LocationId: locationId,
            AmountCents: amountCents.Value,
            CurrencyCode: currencyCode,
            RawStatus: status,
            CreatedAtUtc: createdAt,
            UpdatedAtUtc: updatedAt,
            RawPaymentJson: payment.GetRawText()
        );
    }

    private string GetBaseUrl()
    {
        var configured = configuration["Square:ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return "https://connect.squareup.com";
        }

        return configured.Trim().TrimEnd('/');
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var apiVersion = configuration["Square:ApiVersion"];
        if (!string.IsNullOrWhiteSpace(apiVersion))
        {
            request.Headers.TryAddWithoutValidation("Square-Version", apiVersion.Trim());
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken
    )
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await httpClient.SendAsync(requestFactory(), cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return response;
                }

                if (!IsTransient(response.StatusCode))
                {
                    return response;
                }

                var delay = GetRetryDelay(response, attempt);
                logger.LogWarning(
                    "Square API transient response {StatusCode}; retrying in {DelaySeconds:0.0}s (attempt {Attempt}/{Max})",
                    (int)response.StatusCode,
                    delay.TotalSeconds,
                    attempt,
                    maxAttempts
                );

                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(
                    ex,
                    "Square API request error; retrying in {DelaySeconds:0.0}s (attempt {Attempt}/{Max})",
                    delay.TotalSeconds,
                    attempt,
                    maxAttempts
                );

                response?.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
        }

        // Final attempt: return/throw whatever the last request produces.
        return await httpClient.SendAsync(requestFactory(), cancellationToken);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.InternalServerError
            || statusCode == HttpStatusCode.BadGateway
            || statusCode == HttpStatusCode.ServiceUnavailable
            || statusCode == HttpStatusCode.GatewayTimeout;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            if (
                response.Headers.TryGetValues("Retry-After", out var values)
                && values.FirstOrDefault() is string raw
                && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                && seconds > 0
            )
            {
                return TimeSpan.FromSeconds(Math.Min(seconds, 30));
            }
        }

        return TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 10));
    }

    public sealed record SquarePaymentsPage(IReadOnlyList<SquarePaymentListItem> Payments, string? Cursor);

    public sealed record SquarePaymentListItem(string PaymentId, DateTimeOffset? UpdatedAtUtc);

    public sealed record SquarePaymentDetail(
        string PaymentId,
        string? CustomerId,
        string? LocationId,
        long AmountCents,
        string CurrencyCode,
        string RawStatus,
        DateTimeOffset? CreatedAtUtc,
        DateTimeOffset? UpdatedAtUtc,
        string RawPaymentJson
    );

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement obj)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            obj = default;
            return false;
        }

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

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            array = default;
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            array = property.Value;
            return true;
        }

        array = default;
        return false;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }

            if (property.Value.ValueKind == JsonValueKind.Number)
            {
                return property.Value.GetRawText();
            }
        }

        return null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var i))
            {
                return i;
            }

            if (property.Value.ValueKind == JsonValueKind.String && long.TryParse(property.Value.GetString(), out var s))
            {
                return s;
            }
        }

        return null;
    }

    private static DateTimeOffset? ParseUtcTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (
            DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto
            )
        )
        {
            return dto.ToUniversalTime();
        }

        return null;
    }
}

