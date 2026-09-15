using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class DownloadOrchestratorTests
{
    [Fact]
    public async Task RunAsync_ProducesSummary_WithMixedOutcomes_AndProcessesEveryEntryExactlyOnce()
    {
        var entries = new[]
        {
            new ModEntry(ModKind.Mod, 1, "mod-a", "Mod A", "1.0.0"),
            new ModEntry(ModKind.Mod, 2, "mod-b", "Mod B", "2.0.0"),
            new ModEntry(ModKind.Mod, 3, "mod-c", "Mod C", "3.0.0"),
        };

        var apiClient = new StubForgeApiClient(entry => entry.Id == 3
            ? null
            : new ResolvedDownload(entry, entry.Version, $"https://example.com/{entry.Slug}.zip", 100, null));

        var downloader = new StubFileDownloader(
            findExisting: _ => null,
            download: entry => entry.Id == 2
                ? new DownloadOutcome(DownloadStatus.Skipped, "/tmp/mod-b-2.0.0.zip", null)
                : new DownloadOutcome(DownloadStatus.Downloaded, $"/tmp/{entry.Slug}.zip", null));

        var writer = new StringWriter();
        var orchestrator = new DownloadOrchestrator(apiClient, downloader, writer, maxParallelism: 2);

        var summary = await orchestrator.RunAsync(entries, "/tmp/out", CancellationToken.None);

        Assert.Equal(3, summary.Total);
        Assert.Equal(1, summary.Downloaded);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(1, summary.Failed);
        Assert.Single(summary.FailedItems);
        Assert.Equal("Mod C", summary.FailedItems[0].Entry.Name);
        Assert.Contains("версия не найдена", summary.FailedItems[0].ErrorMessage);
        Assert.Equal(3, downloader.DownloadCallCount + apiClient.FailedCallCount(entries));
    }

    [Fact]
    public async Task RunAsync_SkipsWithoutCallingApi_WhenFileAlreadyExistsOnDisk()
    {
        var entries = new[]
        {
            new ModEntry(ModKind.Mod, 1, "mod-a", "Mod A", "1.0.0"),
        };

        var apiClient = new StubForgeApiClient(
            entry => new ResolvedDownload(entry, entry.Version, $"https://example.com/{entry.Slug}.zip", 100, null));

        var downloader = new StubFileDownloader(
            findExisting: _ => new DownloadOutcome(DownloadStatus.Skipped, "/tmp/mod-a-1.0.0.zip", null),
            download: entry => new DownloadOutcome(DownloadStatus.Downloaded, $"/tmp/{entry.Slug}.zip", null));

        var writer = new StringWriter();
        var orchestrator = new DownloadOrchestrator(apiClient, downloader, writer);

        var summary = await orchestrator.RunAsync(entries, "/tmp/out", CancellationToken.None);

        Assert.Equal(1, summary.Skipped);
        Assert.Equal(0, apiClient.CallCount);
        Assert.Equal(0, downloader.DownloadCallCount);
    }
}

internal sealed class StubForgeApiClient : IForgeApiClient
{
    private readonly Func<ModEntry, ResolvedDownload?> _resolve;
    public int CallCount { get; private set; }

    public StubForgeApiClient(Func<ModEntry, ResolvedDownload?> resolve) => _resolve = resolve;

    public Task<ResolvedDownload?> ResolveDownloadAsync(ModEntry entry, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_resolve(entry));
    }

    public int FailedCallCount(IEnumerable<ModEntry> entries) => entries.Count(e => _resolve(e) is null);
}

internal sealed class StubFileDownloader : IFileDownloader
{
    private readonly Func<ModEntry, DownloadOutcome?> _findExisting;
    private readonly Func<ModEntry, DownloadOutcome> _download;
    public int DownloadCallCount { get; private set; }

    public StubFileDownloader(Func<ModEntry, DownloadOutcome?> findExisting, Func<ModEntry, DownloadOutcome> download)
    {
        _findExisting = findExisting;
        _download = download;
    }

    public DownloadOutcome? FindExisting(ModEntry entry, string outputDirectory) => _findExisting(entry);

    public Task<DownloadOutcome> DownloadAsync(
        ResolvedDownload resolved, string outputDirectory, CancellationToken cancellationToken)
    {
        DownloadCallCount++;
        return Task.FromResult(_download(resolved.Entry));
    }
}
