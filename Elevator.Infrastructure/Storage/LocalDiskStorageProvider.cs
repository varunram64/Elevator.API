using Elevator.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Elevator.Infrastructure.Storage;

/// <summary>
/// Storage provider that downloads files from HTTP(S) URLs to local disk.
/// Streams in configurable chunks and reports progress after every chunk.
///
/// Singleton — HttpClient is managed via IHttpClientFactory internally.
/// </summary>
public sealed class LocalDiskStorageProvider : IStorageProvider
{
    private const int BufferSize = 81_920; // 80 KB

    private readonly IHttpClientFactory          _httpFactory;
    private readonly ILogger<LocalDiskStorageProvider> _logger;

    // MIME → extension lookup
    private static readonly Dictionary<string, string> MimeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"]                                                         = ".pdf",
        ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]      = ".xlsx",
        ["application/vnd.ms-excel"]                                                = ".xls",
        ["text/csv"]                                                                = ".csv",
        ["application/json"]                                                        = ".json",
        ["application/zip"]                                                         = ".zip",
        ["image/png"]                                                               = ".png",
        ["image/jpeg"]                                                              = ".jpg",
        ["application/octet-stream"]                                                = ".bin",
    };

    public LocalDiskStorageProvider(
        IHttpClientFactory httpFactory,
        ILogger<LocalDiskStorageProvider> logger)
    {
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    public async Task<string> DownloadToLocalAsync(
        string                                         sourceUrl,
        string                                         destinationFolder,
        IProgress<(long bytesRead, long? totalBytes)> progress,
        CancellationToken                              ct = default)
    {
        Directory.CreateDirectory(destinationFolder);

        var client   = _httpFactory.CreateClient("downloader");
        using var response = await client.GetAsync(sourceUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? totalBytes   = response.Content.Headers.ContentLength;
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

        // Build local file name from URL + MIME type
        string ext      = MimeExtensions.GetValueOrDefault(contentType, ".bin");
        string fileName = Path.GetFileName(new Uri(sourceUrl).LocalPath);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
            fileName = $"{Path.GetFileNameWithoutExtension(fileName)}{ext}";

        string localPath = Path.Combine(destinationFolder, $"{Guid.NewGuid()}_{fileName}");

        await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream     = new FileStream(localPath, FileMode.Create, FileAccess.Write,
                                             FileShare.None, BufferSize, useAsync: true);

        var buffer       = new byte[BufferSize];
        long totalRead   = 0;
        int  bytesRead;

        while ((bytesRead = await responseStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;
            progress.Report((totalRead, totalBytes));
        }

        _logger.LogInformation("Saved {Bytes:N0} bytes → '{Path}'", totalRead, localPath);
        return localPath;
    }

    public async Task<(Stream stream, string contentType, long? length)> OpenReadAsync(
        string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Downloaded file not found at '{path}'.");

        var info        = new FileInfo(path);
        var stream      = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                              BufferSize, useAsync: true);
        string mimeType = GetMimeType(path);
        return await Task.FromResult((stream as Stream, mimeType, (long?)info.Length));
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        => Task.FromResult(File.Exists(path));

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf"  => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls"  => "application/vnd.ms-excel",
            ".csv"  => "text/csv",
            ".json" => "application/json",
            ".zip"  => "application/zip",
            ".png"  => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _       => "application/octet-stream"
        };
}
