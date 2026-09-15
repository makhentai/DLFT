using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class ArchiveFormatSnifferTests
{
    [Fact]
    public void Detect_ReturnsZip_ForPkSignature()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00 };
        using var stream = new MemoryStream(bytes);

        var format = ArchiveFormatSniffer.Detect(stream);

        Assert.Equal(DetectedArchiveFormat.Zip, format);
    }

    [Fact]
    public void Detect_ReturnsZip_ForEmptyZipEndOfCentralDirectorySignature()
    {
        var bytes = new byte[] { 0x50, 0x4B, 0x05, 0x06, 0x00, 0x00 };
        using var stream = new MemoryStream(bytes);

        var format = ArchiveFormatSniffer.Detect(stream);

        Assert.Equal(DetectedArchiveFormat.Zip, format);
    }

    [Fact]
    public void Detect_ReturnsSevenZip_For7zSignature()
    {
        var bytes = new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x04 };
        using var stream = new MemoryStream(bytes);

        var format = ArchiveFormatSniffer.Detect(stream);

        Assert.Equal(DetectedArchiveFormat.SevenZip, format);
    }

    [Fact]
    public void Detect_ReturnsUnknown_ForUnrecognizedContent()
    {
        var bytes = "this is not really not an archive at all"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        var format = ArchiveFormatSniffer.Detect(stream);

        Assert.Equal(DetectedArchiveFormat.Unknown, format);
    }

    [Fact]
    public void Detect_ReturnsUnknown_ForEmptyStream()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());

        var format = ArchiveFormatSniffer.Detect(stream);

        Assert.Equal(DetectedArchiveFormat.Unknown, format);
    }

    [Fact]
    public void Detect_ReturnsUnknown_ForTooShortStream()
    {
        var bytes = new byte[] { 0x37, 0x7A };
        using var stream = new MemoryStream(bytes);

        var format = ArchiveFormatSniffer.Detect(stream);

        Assert.Equal(DetectedArchiveFormat.Unknown, format);
    }
}
