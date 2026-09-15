using System.Net.Http.Headers;

namespace DownloadListFromTushenka;

public enum DownloadStatus
{
    Downloaded,
    Skipped,
    Failed
}

public sealed record DownloadOutcome(DownloadStatus Status, string? FilePath, string? ErrorMessage);

public interface IFileDownloader
{
    Task<DownloadOutcome> DownloadAsync(
        ResolvedDownload resolved, string outputDirectory, CancellationToken cancellationToken);

    /// <summary>
    /// Ищет на диске файл, уже скачанный для этой записи ранее (по маске
    /// "{slug}-{version}.*"), не обращаясь к сети/API. Используется, чтобы
    /// на повторных запусках не дёргать API за версией мода, который уже есть.
    /// </summary>
    DownloadOutcome? FindExisting(ModEntry entry, string outputDirectory);
}

public sealed class FileDownloader : IFileDownloader
{
    private readonly HttpClient _httpClient;

    public FileDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public DownloadOutcome? FindExisting(ModEntry entry, string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return null;
        }

        var pattern = $"{entry.Slug}-{entry.Version}.*";
        var match = Directory.EnumerateFiles(outputDirectory, pattern).FirstOrDefault();
        return match is null ? null : new DownloadOutcome(DownloadStatus.Skipped, match, null);
    }

    public async Task<DownloadOutcome> DownloadAsync(
        ResolvedDownload resolved, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        try
        {
            using var response = await _httpClient.GetAsync(
                resolved.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var extension = DetermineExtension(response.Content.Headers.ContentDisposition, response.RequestMessage?.RequestUri);
            var fileName = $"{resolved.Entry.Slug}-{resolved.ResolvedVersion}{extension}";
            var filePath = Path.Combine(outputDirectory, fileName);

            if (File.Exists(filePath))
            {
                return new DownloadOutcome(DownloadStatus.Skipped, filePath, null);
            }

            await using (var fileStream = File.Create(filePath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            return new DownloadOutcome(DownloadStatus.Downloaded, filePath, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return new DownloadOutcome(DownloadStatus.Failed, null, ex.Message);
        }
    }

    internal static string DetermineExtension(ContentDispositionHeaderValue? contentDisposition, Uri? finalUri)
    {
        var fromHeader = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        if (!string.IsNullOrWhiteSpace(fromHeader))
        {
            var trimmed = fromHeader.Trim('"');
            var ext = Path.GetExtension(trimmed);
            if (!string.IsNullOrEmpty(ext))
            {
                return ext;
            }
        }

        if (finalUri is not null)
        {
            var ext = Path.GetExtension(finalUri.AbsolutePath);
            if (!string.IsNullOrEmpty(ext))
            {
                return ext;
            }
        }

        return ".zip";
    }
}
