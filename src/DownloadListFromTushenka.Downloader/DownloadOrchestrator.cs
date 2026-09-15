namespace DownloadListFromTushenka;

public sealed record ItemResult(ModEntry Entry, DownloadStatus Status, string? ErrorMessage, string? Warning = null);

public sealed record RunSummary(
    int Total,
    int Downloaded,
    int Skipped,
    int Failed,
    IReadOnlyList<ItemResult> FailedItems);

public sealed class DownloadOrchestrator
{
    private readonly IForgeApiClient _apiClient;
    private readonly IFileDownloader _fileDownloader;
    private readonly TextWriter _output;
    private readonly int _maxParallelism;

    public DownloadOrchestrator(
        IForgeApiClient apiClient,
        IFileDownloader fileDownloader,
        TextWriter output,
        int maxParallelism = 4)
    {
        _apiClient = apiClient;
        _fileDownloader = fileDownloader;
        _output = output;
        _maxParallelism = maxParallelism;
    }

    public async Task<RunSummary> RunAsync(
        IReadOnlyList<ModEntry> entries, string outputDirectory, CancellationToken cancellationToken)
    {
        var results = new ItemResult[entries.Count];
        var nextIndex = 0;
        var completed = 0;
        var progressLock = new object();

        async Task WorkerAsync()
        {
            while (true)
            {
                int index;
                lock (progressLock)
                {
                    if (nextIndex >= entries.Count)
                    {
                        return;
                    }
                    index = nextIndex++;
                }

                var result = await ProcessEntryAsync(entries[index], outputDirectory, cancellationToken);
                results[index] = result;

                lock (progressLock)
                {
                    completed++;
                    _output.WriteLine(FormatProgressLine(completed, entries.Count, result));
                }
            }
        }

        var workerCount = Math.Min(_maxParallelism, Math.Max(1, entries.Count));
        var workers = Enumerable.Range(0, workerCount).Select(_ => WorkerAsync());
        await Task.WhenAll(workers);

        var downloaded = results.Count(r => r.Status == DownloadStatus.Downloaded);
        var skipped = results.Count(r => r.Status == DownloadStatus.Skipped);
        var failed = results.Where(r => r.Status == DownloadStatus.Failed).ToList();

        return new RunSummary(entries.Count, downloaded, skipped, failed.Count, failed);
    }

    private async Task<ItemResult> ProcessEntryAsync(
        ModEntry entry, string outputDirectory, CancellationToken cancellationToken)
    {
        var existing = _fileDownloader.FindExisting(entry, outputDirectory);
        if (existing is not null)
        {
            return new ItemResult(entry, existing.Status, existing.ErrorMessage);
        }

        var resolved = await _apiClient.ResolveDownloadAsync(entry, cancellationToken);
        if (resolved is null)
        {
            return new ItemResult(entry, DownloadStatus.Failed, "версия не найдена в API");
        }

        var outcome = await _fileDownloader.DownloadAsync(resolved, outputDirectory, cancellationToken);
        return new ItemResult(entry, outcome.Status, outcome.ErrorMessage, resolved.Warning);
    }

    private static string FormatProgressLine(int completed, int total, ItemResult result)
    {
        var status = result.Status switch
        {
            DownloadStatus.Downloaded => "OK",
            DownloadStatus.Skipped => "SKIP (уже скачан)",
            DownloadStatus.Failed => $"ОШИБКА: {result.ErrorMessage}",
            _ => result.Status.ToString()
        };
        var warningSuffix = string.IsNullOrEmpty(result.Warning) ? "" : $" [ВНИМАНИЕ: {result.Warning}]";
        return $"[{completed}/{total}] {result.Entry.Name} {result.Entry.Version} ... {status}{warningSuffix}";
    }
}
