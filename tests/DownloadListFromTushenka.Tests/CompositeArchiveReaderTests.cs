using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class CompositeArchiveReaderTests : IDisposable
{
    private readonly string _workDir =
        Path.Combine(Path.GetTempPath(), "dlft-composite-" + Guid.NewGuid().ToString("N"));

    public CompositeArchiveReaderTests()
    {
        Directory.CreateDirectory(_workDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workDir))
        {
            Directory.Delete(_workDir, recursive: true);
        }
    }

    private string WriteFile(string fileName, byte[] content)
    {
        var path = Path.Combine(_workDir, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void ListEntryPaths_UsesZipReader_ForZipExtensionWithZipContent()
    {
        var path = WriteFile("mod.zip", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0 });
        var zipReader = new RecordingArchiveReader();
        var sevenZipReader = new RecordingArchiveReader();
        var composite = new CompositeArchiveReader(zipReader, sevenZipReader);

        composite.ListEntryPaths(path);

        Assert.Equal(1, zipReader.ListCallCount);
        Assert.Equal(0, sevenZipReader.ListCallCount);
    }

    [Fact]
    public void ListEntryPaths_UsesSevenZipReader_ForSevenZipExtensionWithSevenZipContent()
    {
        var path = WriteFile("mod.7z", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0, 4 });
        var zipReader = new RecordingArchiveReader();
        var sevenZipReader = new RecordingArchiveReader();
        var composite = new CompositeArchiveReader(zipReader, sevenZipReader);

        composite.ListEntryPaths(path);

        Assert.Equal(0, zipReader.ListCallCount);
        Assert.Equal(1, sevenZipReader.ListCallCount);
    }

    [Fact]
    public void ListEntryPaths_TrustsContentOverExtension_WhenAZipFileIsNamedDotSevenZ()
    {
        // Реальный случай: мод выложен как обычный zip, но файл релиза на
        // GitHub назван с расширением .7z. Библиотека 7z.dll отказывается
        // такое открывать — нужно распознать по содержимому и отдать в zip-ридер.
        var path = WriteFile("mislabeled.7z", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0, 0 });
        var zipReader = new RecordingArchiveReader();
        var sevenZipReader = new RecordingArchiveReader();
        var composite = new CompositeArchiveReader(zipReader, sevenZipReader);

        composite.ListEntryPaths(path);

        Assert.Equal(1, zipReader.ListCallCount);
        Assert.Equal(0, sevenZipReader.ListCallCount);
    }

    [Fact]
    public void ListEntryPaths_TrustsContentOverExtension_WhenASevenZipFileIsNamedDotZip()
    {
        var path = WriteFile("mislabeled.zip", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0, 4 });
        var zipReader = new RecordingArchiveReader();
        var sevenZipReader = new RecordingArchiveReader();
        var composite = new CompositeArchiveReader(zipReader, sevenZipReader);

        composite.ListEntryPaths(path);

        Assert.Equal(0, zipReader.ListCallCount);
        Assert.Equal(1, sevenZipReader.ListCallCount);
    }

    [Fact]
    public void ListEntryPaths_FallsBackToExtension_WhenContentSignatureIsUnrecognized()
    {
        var path = WriteFile("mod.zip", "not really an archive, just text"u8.ToArray());
        var zipReader = new RecordingArchiveReader();
        var sevenZipReader = new RecordingArchiveReader();
        var composite = new CompositeArchiveReader(zipReader, sevenZipReader);

        composite.ListEntryPaths(path);

        Assert.Equal(1, zipReader.ListCallCount);
        Assert.Equal(0, sevenZipReader.ListCallCount);
    }

    [Fact]
    public void ExtractTo_ThrowsNotSupportedException_ForUnknownExtensionAndUnknownContent()
    {
        var path = WriteFile("mod.bin", "not an archive"u8.ToArray());
        var composite = new CompositeArchiveReader(new RecordingArchiveReader(), new RecordingArchiveReader());

        Assert.Throws<NotSupportedException>(() => composite.ExtractTo(path, "C:\\dest", false));
    }

    [Fact]
    public void ListEntryPaths_UsesSevenZipReader_ForRarSignature()
    {
        // .7z.dll (пакет SevenZipExtractor) умеет читать RAR на распаковку,
        // поэтому такие архивы направляются в тот же читатель, что и .7z,
        // не оставляя их "неподдерживаемым форматом".
        var path = WriteFile("mod.rar", new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 });
        var zipReader = new RecordingArchiveReader();
        var sevenZipReader = new RecordingArchiveReader();
        var composite = new CompositeArchiveReader(zipReader, sevenZipReader);

        composite.ListEntryPaths(path);

        Assert.Equal(0, zipReader.ListCallCount);
        Assert.Equal(1, sevenZipReader.ListCallCount);
    }

    [Fact]
    public void ExtractTo_UsesSevenZipReader_ForRarExtension_WhenContentUnrecognized()
    {
        var path = WriteFile("mod.rar", "not really an archive"u8.ToArray());
        var zipReader = new RecordingArchiveReader();
        var sevenZipReader = new RecordingArchiveReader();
        var composite = new CompositeArchiveReader(zipReader, sevenZipReader);

        composite.ExtractTo(path, "C:\\dest", false);

        Assert.Equal(0, zipReader.ExtractCallCount);
        Assert.Equal(1, sevenZipReader.ExtractCallCount);
    }
}

internal sealed class RecordingArchiveReader : IArchiveReader
{
    public int ListCallCount { get; private set; }
    public int ExtractCallCount { get; private set; }

    public IReadOnlyList<string> ListEntryPaths(string archivePath)
    {
        ListCallCount++;
        return Array.Empty<string>();
    }

    public ArchiveExtractResult ExtractTo(
        string archivePath, string destinationDirectory, bool overwriteExisting, Func<string, bool>? shouldSkipEntry = null)
    {
        ExtractCallCount++;
        return new ArchiveExtractResult(0, 0);
    }
}
