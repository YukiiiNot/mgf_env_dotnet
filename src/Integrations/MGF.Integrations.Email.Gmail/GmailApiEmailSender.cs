namespace MGF.Integrations.Email.Gmail;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MGF.Contracts.Abstractions.Email;

public sealed class GmailApiEmailSender : IEmailSender
{
    private const string GmailScope = "https://www.googleapis.com/auth/gmail.send";

    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static string? CachedAccessToken;
    private static DateTimeOffset CachedExpiresAt;

    private readonly IConfiguration configuration;
    private readonly ILogger? logger;
    private readonly HttpClient httpClient = new();

    public GmailApiEmailSender(IConfiguration configuration, ILogger? logger = null)
    {
        this.configuration = configuration;
        this.logger = logger;
        httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage request,
        CancellationToken cancellationToken)
    {
        var enabled = configuration.GetValue("Integrations:Email:Enabled", false);
        if (!enabled)
        {
            return Failed(request, "Email sending disabled (Integrations:Email:Enabled=false).");
        }

        var profile = EmailProfileResolver.Resolve(configuration, request.ProfileKey);
        if (!EmailProfileResolver.IsAllowedFrom(profile, request.FromAddress))
        {
            var allowed = EmailProfileResolver.AllowedFromDisplay(profile);
            return Failed(request, $"Gmail fromAddress must be {allowed}.");
        }

        var serviceAccount = configuration["Integrations:Email:Gmail:ServiceAccountEmail"]
            ?? configuration["Email:Gmail:ServiceAccountEmail"];
        var privateKey = configuration["Integrations:Email:Gmail:PrivateKey"]
            ?? configuration["Email:Gmail:PrivateKey"];
        var impersonatedUser = configuration["Integrations:Email:Gmail:ImpersonatedUser"]
            ?? configuration["Email:Gmail:ImpersonatedUser"];
        var tokenUri = configuration["Integrations:Email:Gmail:TokenUri"]
            ?? configuration["Email:Gmail:TokenUri"]
            ?? "https://oauth2.googleapis.com/token";

        if (string.IsNullOrWhiteSpace(serviceAccount) || string.IsNullOrWhiteSpace(privateKey))
        {
            return Failed(request, "Gmail API not configured (service account or private key missing).");
        }

        if (string.IsNullOrWhiteSpace(impersonatedUser))
        {
            return Failed(request, "Gmail API impersonated user not configured.");
        }

        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(
                serviceAccount,
                privateKey,
                impersonatedUser,
                tokenUri,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return Failed(request, $"Gmail API token failed: {ex.Message}");
        }

        try
        {
            var raw = BuildRawMessage(request);
            var encoded = Base64UrlEncode(Encoding.UTF8.GetBytes(raw));
            var payload = JsonSerializer.Serialize(new { raw = encoded });

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Failed(request, $"Gmail send failed: {(int)response.StatusCode} {json}");
            }

            string? messageId = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    messageId = idProp.GetString();
                }
            }
            catch
            {
                // ignore parse errors
            }

            return new EmailSendResult(
                Status: "sent",
                Provider: "gmail",
                FromAddress: request.FromAddress,
                To: request.To,
                Subject: request.Subject,
                SentAtUtc: DateTimeOffset.UtcNow,
                ProviderMessageId: messageId,
                Error: null,
                TemplateVersion: request.TemplateVersion,
                ReplyTo: request.ReplyTo);
        }
        catch (Exception ex)
        {
            return Failed(request, $"Gmail send failed: {ex.Message}");
        }
    }

    private async Task<string> GetAccessTokenAsync(
        string serviceAccountEmail,
        string privateKeyPem,
        string impersonatedUser,
        string tokenUri,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(CachedAccessToken) && CachedExpiresAt > now.AddSeconds(30))
        {
            return CachedAccessToken;
        }

        await TokenLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(CachedAccessToken) && CachedExpiresAt > now.AddSeconds(30))
            {
                return CachedAccessToken;
            }

            var jwt = BuildJwt(serviceAccountEmail, privateKeyPem, impersonatedUser, tokenUri, now);
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "assertion", jwt }
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUri)
            {
                Content = body
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Gmail token response {(int)response.StatusCode}: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var access = doc.RootElement.GetProperty("access_token").GetString();
            if (string.IsNullOrWhiteSpace(access))
            {
                throw new InvalidOperationException("Gmail token response missing access_token.");
            }

            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expiresElement)
                ? expiresElement.GetInt32()
                : 3600;
            var safeSeconds = Math.Max(30, expiresIn - 60);
            CachedAccessToken = access;
            CachedExpiresAt = DateTimeOffset.UtcNow.AddSeconds(safeSeconds);
            return access;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    private static string BuildJwt(
        string serviceAccountEmail,
        string privateKeyPem,
        string impersonatedUser,
        string tokenUri,
        DateTimeOffset now)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
        var iat = now.ToUnixTimeSeconds();
        var exp = now.AddMinutes(60).ToUnixTimeSeconds();
        var claimJson = JsonSerializer.Serialize(new
        {
            iss = serviceAccountEmail,
            scope = GmailScope,
            aud = tokenUri,
            exp,
            iat,
            sub = impersonatedUser
        });
        var payload = Base64UrlEncode(Encoding.UTF8.GetBytes(claimJson));
        var unsigned = header + "." + payload;

        var pem = NormalizePem(privateKeyPem);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var signature = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var sig = Base64UrlEncode(signature);
        return unsigned + "." + sig;
    }

    private static string NormalizePem(string pem)
    {
        if (pem.Contains("\\n", StringComparison.Ordinal))
        {
            return pem.Replace("\\n", "\n");
        }

        return pem;
    }

    private static string BuildRawMessage(EmailMessage request)
    {
        var boundary = "alt_" + Guid.NewGuid().ToString("N");
        var builder = new StringBuilder();
        builder.AppendLine($"From: {FormatFromHeader(request)}");
        builder.AppendLine($"To: {string.Join(", ", request.To)}");
        if (!string.IsNullOrWhiteSpace(request.ReplyTo))
        {
            builder.AppendLine($"Reply-To: {request.ReplyTo}");
        }

        builder.AppendLine($"Subject: {request.Subject}");
        builder.AppendLine("MIME-Version: 1.0");
        if (!string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            builder.AppendLine($"Content-Type: multipart/alternative; boundary=\"{boundary}\"");
            builder.AppendLine();
            builder.AppendLine($"--{boundary}");
            builder.AppendLine("Content-Type: text/plain; charset=UTF-8");
            builder.AppendLine();
            builder.AppendLine(request.BodyText);
            builder.AppendLine();
            builder.AppendLine($"--{boundary}");
            builder.AppendLine("Content-Type: text/html; charset=UTF-8");
            builder.AppendLine();
            builder.AppendLine(request.HtmlBody);
            builder.AppendLine();
            builder.AppendLine($"--{boundary}--");
        }
        else
        {
            builder.AppendLine("Content-Type: text/plain; charset=UTF-8");
            builder.AppendLine();
            builder.AppendLine(request.BodyText);
        }
        return builder.ToString();
    }

    private static string FormatFromHeader(EmailMessage request)
    {
        if (string.IsNullOrWhiteSpace(request.FromName))
        {
            return request.FromAddress;
        }

        var safeName = request.FromName.Replace("\"", string.Empty);
        return $"\"{safeName}\" <{request.FromAddress}>";
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static EmailSendResult Failed(EmailMessage request, string error)
    {
        return new EmailSendResult(
            Status: "failed",
            Provider: "gmail",
            FromAddress: request.FromAddress,
            To: request.To,
            Subject: request.Subject,
            SentAtUtc: null,
            ProviderMessageId: null,
            Error: error,
            TemplateVersion: request.TemplateVersion,
            ReplyTo: request.ReplyTo);
    }
}
