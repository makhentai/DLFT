using System.Net;
using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class FileDownloaderTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "dlft-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_SavesFile_WithExtensionFromFinalRedirectedUrl()
    {
        var bytes = "fake-zip-content"u8.ToArray();
        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
            RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                "https://github.com/Hysocs/ragdollkinetics-spt/releases/download/1.2.0/RagdollKinetics-1.2.0.zip")
        });
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "1.2.0");
        var resolved = new ResolvedDownload(
            entry, "1.2.0", "https://sp-mod.com/mod/download/2905/ragdoll-kinetics/1.2.0", bytes.Length, null);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Downloaded, outcome.Status);
        var expectedPath = Path.Combine(_tempDir, "ragdoll-kinetics-1.2.0.zip");
        Assert.Equal(expectedPath, outcome.FilePath);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(expectedPath));
    }

    [Fact]
    public async Task DownloadAsync_FallsBackToZipExtension_WhenUrlHasNoExtension()
    {
        var bytes = "content"u8.ToArray();
        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/download/no-extension-here")
        });
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 1, "some-mod", "Some Mod", "2.0.0");
        var resolved = new ResolvedDownload(entry, "2.0.0", "https://sp-mod.com/mod/download/1/some-mod/2.0.0", null, null);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Downloaded, outcome.Status);
        Assert.Equal(Path.Combine(_tempDir, "some-mod-2.0.0.zip"), outcome.FilePath);
    }

    [Fact]
    public async Task DownloadAsync_SkipsExistingFile_WithoutOverwritingIt()
    {
        Directory.CreateDirectory(_tempDir);
        var existingPath = Path.Combine(_tempDir, "ragdoll-kinetics-1.2.0.zip");
        await File.WriteAllTextAsync(existingPath, "already here");

        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("new-content"u8.ToArray()),
            RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                "https://github.com/example/releases/download/1.2.0/RagdollKinetics-1.2.0.zip")
        });
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "1.2.0");
        var resolved = new ResolvedDownload(
            entry, "1.2.0", "https://sp-mod.com/mod/download/2905/ragdoll-kinetics/1.2.0", null, null);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Skipped, outcome.Status);
        Assert.Equal("already here", await File.ReadAllTextAsync(existingPath));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsFailed_WhenHttpRequestFails()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 1, "broken-mod", "Broken Mod", "1.0.0");
        var resolved = new ResolvedDownload(entry, "1.0.0", "https://sp-mod.com/mod/download/1/broken-mod/1.0.0", null, null);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Failed, outcome.Status);
        Assert.NotNull(outcome.ErrorMessage);
    }

    [Fact]
    public void FindExisting_ReturnsSkipped_WhenMatchingFileIsOnDisk()
    {
        Directory.CreateDirectory(_tempDir);
        var existingPath = Path.Combine(_tempDir, "ragdoll-kinetics-1.2.0.zip");
        File.WriteAllText(existingPath, "already here");
        var downloader = new FileDownloader(new HttpClient());
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "1.2.0");

        var outcome = downloader.FindExisting(entry, _tempDir);

        Assert.NotNull(outcome);
        Assert.Equal(DownloadStatus.Skipped, outcome!.Status);
        Assert.Equal(existingPath, outcome.FilePath);
    }

    [Fact]
    public void FindExisting_ReturnsNull_WhenNoMatchingFileExists()
    {
        var downloader = new FileDownloader(new HttpClient());
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "1.2.0");

        var outcome = downloader.FindExisting(entry, _tempDir);

        Assert.Null(outcome);
    }
}
