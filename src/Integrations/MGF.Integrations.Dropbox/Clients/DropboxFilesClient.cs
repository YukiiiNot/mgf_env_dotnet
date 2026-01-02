namespace MGF.Integrations.Dropbox;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MGF.Contracts.Abstractions.Dropbox;

public sealed class DropboxFilesClient : IDropboxFilesClient
{
    private const int UploadSessionThresholdBytes = 150 * 1024 * 1024;
    private const int UploadChunkSizeBytes = 8 * 1024 * 1024;

    private readonly HttpClient httpClient;
    private readonly string apiBaseUrl;
    private readonly string contentBaseUrl;

    public DropboxFilesClient(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        apiBaseUrl = configuration["Integrations:Dropbox:ApiBaseUrl"] ?? "https://api.dropboxapi.com/2";
        contentBaseUrl = configuration["Integrations:Dropbox:ContentBaseUrl"] ?? "https://content.dropboxapi.com/2";
    }

    public async Task EnsureFolderAsync(string accessToken, string dropboxPath, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            path = dropboxPath,
            autorename = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl}/files/create_folder_v2");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.Conflict
            && body.Contains("path/conflict", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException($"Dropbox create_folder_v2 failed: {(int)response.StatusCode} {body}");
    }

    public async Task UploadFileAsync(string accessToken, string dropboxPath, string localFilePath, CancellationToken cancellationToken)
    {
        var info = new FileInfo(localFilePath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("Upload source file not found.", localFilePath);
        }

        if (info.Length <= UploadSessionThresholdBytes)
        {
            await UploadSmallFileAsync(accessToken, dropboxPath, info, cancellationToken);
            return;
        }

        await UploadLargeFileAsync(accessToken, dropboxPath, info, cancellationToken);
    }

    public Task UploadBytesAsync(string accessToken, string dropboxPath, byte[] content, CancellationToken cancellationToken)
    {
        var stream = new MemoryStream(content);
        return UploadContentAsync(accessToken, dropboxPath, stream, cancellationToken);
    }

    private async Task UploadSmallFileAsync(string accessToken, string dropboxPath, FileInfo info, CancellationToken cancellationToken)
    {
        await using var stream = info.OpenRead();
        await UploadContentAsync(accessToken, dropboxPath, stream, cancellationToken);
    }

    private async Task UploadContentAsync(string accessToken, string dropboxPath, Stream stream, CancellationToken cancellationToken)
    {
        var arg = JsonSerializer.Serialize(new
        {
            path = dropboxPath,
            mode = "overwrite",
            autorename = false,
            mute = true,
            strict_conflict = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{contentBaseUrl}/files/upload");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Dropbox-API-Arg", arg);
        request.Content = new StreamContent(stream);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Dropbox files/upload failed: {(int)response.StatusCode} {body}");
    }

    private async Task UploadLargeFileAsync(string accessToken, string dropboxPath, FileInfo info, CancellationToken cancellationToken)
    {
        await using var stream = info.OpenRead();
        var buffer = new byte[UploadChunkSizeBytes];

        var firstRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        if (firstRead == 0)
        {
            await UploadContentAsync(accessToken, dropboxPath, Stream.Null, cancellationToken);
            return;
        }

        var sessionId = await StartSessionAsync(accessToken, buffer, firstRead, cancellationToken);
        var offset = firstRead;

        while (offset < info.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            var isLast = offset + read >= info.Length;
            if (isLast)
            {
                await FinishSessionAsync(accessToken, sessionId, offset, buffer, read, dropboxPath, cancellationToken);
            }
            else
            {
                await AppendSessionAsync(accessToken, sessionId, offset, buffer, read, cancellationToken);
            }

            offset += read;
        }
    }

    private async Task<string> StartSessionAsync(string accessToken, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var arg = JsonSerializer.Serialize(new { close = false });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{contentBaseUrl}/files/upload_session/start");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Dropbox-API-Arg", arg);
        request.Content = new ByteArrayContent(buffer, 0, count);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Dropbox upload_session/start failed: {(int)response.StatusCode} {body}");
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("session_id", out var sessionIdElement))
        {
            throw new InvalidOperationException("Dropbox upload_session/start response missing session_id.");
        }

        return sessionIdElement.GetString() ?? throw new InvalidOperationException("Dropbox upload session id is empty.");
    }

    private async Task AppendSessionAsync(string accessToken, string sessionId, long offset, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var arg = JsonSerializer.Serialize(new
        {
            cursor = new
            {
                session_id = sessionId,
                offset
            },
            close = false
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{contentBaseUrl}/files/upload_session/append_v2");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Dropbox-API-Arg", arg);
        request.Content = new ByteArrayContent(buffer, 0, count);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Dropbox upload_session/append_v2 failed: {(int)response.StatusCode} {body}");
    }

    private async Task FinishSessionAsync(
        string accessToken,
        string sessionId,
        long offset,
        byte[] buffer,
        int count,
        string dropboxPath,
        CancellationToken cancellationToken)
    {
        var arg = JsonSerializer.Serialize(new
        {
            cursor = new
            {
                session_id = sessionId,
                offset
            },
            commit = new
            {
                path = dropboxPath,
                mode = "overwrite",
                autorename = false,
                mute = true,
                strict_conflict = false
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{contentBaseUrl}/files/upload_session/finish");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Dropbox-API-Arg", arg);
        request.Content = new ByteArrayContent(buffer, 0, count);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Dropbox upload_session/finish failed: {(int)response.StatusCode} {body}");
    }
}
