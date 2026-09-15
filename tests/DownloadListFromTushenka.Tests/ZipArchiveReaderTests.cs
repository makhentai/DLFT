using System.IO.Compression;
using System.Text;
using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class ZipArchiveReaderTests : IDisposable
{
    private readonly string _workDir =
        Path.Combine(Path.GetTempPath(), "dlft-zipreader-" + Guid.NewGuid().ToString("N"));

    public ZipArchiveReaderTests()
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

    private string CreateZip(string fileName, Action<ZipArchive> populate)
    {
        var path = Path.Combine(_workDir, fileName);
        using (var stream = File.Create(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            populate(archive);
        }
        return path;
    }

    [Fact]
    public void ListEntryPaths_ReturnsFileEntries_WithNestedFolders()
    {
        var zipPath = CreateZip("mod.zip", archive =>
        {
            using (var entry1 = archive.CreateEntry("BepInEx/plugins/Mod.dll").Open())
            {
                entry1.Write("dll-content"u8);
            }
            using (var entry2 = archive.CreateEntry("readme.txt").Open())
            {
                entry2.Write("readme"u8);
            }
        });
        var reader = new ZipArchiveReader();

        var paths = reader.ListEntryPaths(zipPath);

        Assert.Contains(paths, p => p.Replace('\\', '/') == "BepInEx/plugins/Mod.dll");
        Assert.Contains(paths, p => p.Replace('\\', '/') == "readme.txt");
    }

    [Fact]
    public void ExtractTo_WritesFiles_PreservingFolderStructure()
    {
        var zipPath = CreateZip("mod.zip", archive =>
        {
            using var entry1 = archive.CreateEntry("BepInEx/plugins/Mod.dll").Open();
            entry1.Write("dll-content"u8);
        });
        var destDir = Path.Combine(_workDir, "dest");
        var reader = new ZipArchiveReader();

        var result = reader.ExtractTo(zipPath, destDir, overwriteExisting: false);

        Assert.Equal(1, result.FilesWritten);
        Assert.Equal(0, result.FilesSkipped);
        var extractedFile = Path.Combine(destDir, "BepInEx", "plugins", "Mod.dll");
        Assert.True(File.Exists(extractedFile));
        Assert.Equal("dll-content", File.ReadAllText(extractedFile));
    }

    [Fact]
    public void ExtractTo_SkipsExistingFile_WhenOverwriteIsFalse()
    {
        var zipPath = CreateZip("mod.zip", archive =>
        {
            using var entry1 = archive.CreateEntry("Mod.dll").Open();
            entry1.Write("new-content"u8);
        });
        var destDir = Path.Combine(_workDir, "dest");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, "Mod.dll"), "old-content");
        var reader = new ZipArchiveReader();

        var result = reader.ExtractTo(zipPath, destDir, overwriteExisting: false);

        Assert.Equal(0, result.FilesWritten);
        Assert.Equal(1, result.FilesSkipped);
        Assert.Equal("old-content", File.ReadAllText(Path.Combine(destDir, "Mod.dll")));
    }

    [Fact]
    public void ExtractTo_OverwritesExistingFile_WhenOverwriteIsTrue()
    {
        var zipPath = CreateZip("mod.zip", archive =>
        {
            using var entry1 = archive.CreateEntry("Mod.dll").Open();
            entry1.Write("new-content"u8);
        });
        var destDir = Path.Combine(_workDir, "dest");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(Path.Combine(destDir, "Mod.dll"), "old-content");
        var reader = new ZipArchiveReader();

        var result = reader.ExtractTo(zipPath, destDir, overwriteExisting: true);

        Assert.Equal(1, result.FilesWritten);
        Assert.Equal(0, result.FilesSkipped);
        Assert.Equal("new-content", File.ReadAllText(Path.Combine(destDir, "Mod.dll")));
    }

    [Fact]
    public void ExtractTo_DoesNotWriteOutsideDestinationDirectory_ForZipSlipEntry()
    {
        var zipPath = CreateZip("evil.zip", archive =>
        {
            using var entry = archive.CreateEntry("../../evil.txt").Open();
            entry.Write("gotcha"u8);
        });
        var destDir = Path.Combine(_workDir, "dest");
        var reader = new ZipArchiveReader();

        var result = reader.ExtractTo(zipPath, destDir, overwriteExisting: true);

        Assert.Equal(0, result.FilesWritten);
        Assert.False(File.Exists(Path.Combine(_workDir, "evil.txt")));
    }

    // System.IO.Compression всегда выставляет ZIP-флаг UTF-8, когда сам
    // создаёт архив с не-ASCII именем — поэтому воспроизвести реальный
    // "легаси" случай (архиватор на Windows пишет кириллицу в cp1251 без
    // флага UTF-8, как часто бывает со старыми/сторонними зиперами) можно
    // только собрав ZIP руками, байт в байт.
    [Fact]
    public void ListEntryPaths_DecodesCyrillicName_WhenUtf8FlagIsNotSet()
    {
        const string entryName = "Патч/бронежилет.dll";
        var zipPath = Path.Combine(_workDir, "legacy-cyrillic.zip");
        WriteRawZipWithSingleStoredEntry(zipPath, entryName, "content"u8.ToArray(), setUtf8Flag: false);
        var reader = new ZipArchiveReader();

        var paths = reader.ListEntryPaths(zipPath);

        Assert.Contains(paths, p => p.Replace('\\', '/') == entryName);
    }

    [Fact]
    public void ExtractTo_WritesFile_WithCyrillicNameAndPath_WhenUtf8FlagIsNotSet()
    {
        const string entryName = "Патч/бронежилет.dll";
        var zipPath = Path.Combine(_workDir, "legacy-cyrillic.zip");
        var content = "dll-content"u8.ToArray();
        WriteRawZipWithSingleStoredEntry(zipPath, entryName, content, setUtf8Flag: false);
        var destDir = Path.Combine(_workDir, "dest");
        var reader = new ZipArchiveReader();

        var result = reader.ExtractTo(zipPath, destDir, overwriteExisting: false);

        Assert.Equal(1, result.FilesWritten);
        var extractedFile = Path.Combine(destDir, "Патч", "бронежилет.dll");
        Assert.True(File.Exists(extractedFile));
        Assert.Equal(content, File.ReadAllBytes(extractedFile));
    }

    /// <summary>
    /// Собирает минимальный ZIP (один файл, метод Stored) вручную, чтобы
    /// напрямую контролировать general purpose flag и кодировку байтов
    /// имени — то, что System.IO.Compression при создании архива не даёт
    /// подделать (оно само решает, когда ставить флаг UTF-8).
    /// </summary>
    private static void WriteRawZipWithSingleStoredEntry(
        string path, string entryName, byte[] content, bool setUtf8Flag)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var nameBytes = setUtf8Flag ? Encoding.UTF8.GetBytes(entryName) : Encoding.GetEncoding(1251).GetBytes(entryName);
        ushort flags = setUtf8Flag ? (ushort)0x0800 : (ushort)0x0000;
        var crc = Crc32(content);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(stream);

        var localHeaderOffset = (uint)stream.Position;

        w.Write(0x04034b50u);
        w.Write((ushort)20);
        w.Write(flags);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write(crc);
        w.Write((uint)content.Length);
        w.Write((uint)content.Length);
        w.Write((ushort)nameBytes.Length);
        w.Write((ushort)0);
        w.Write(nameBytes);
        w.Write(content);

        var centralDirOffset = (uint)stream.Position;

        w.Write(0x02014b50u);
        w.Write((ushort)20);
        w.Write((ushort)20);
        w.Write(flags);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write(crc);
        w.Write((uint)content.Length);
        w.Write((uint)content.Length);
        w.Write((ushort)nameBytes.Length);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((uint)0);
        w.Write(localHeaderOffset);
        w.Write(nameBytes);

        var centralDirSize = (uint)stream.Position - centralDirOffset;

        w.Write(0x06054b50u);
        w.Write((ushort)0);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)1);
        w.Write(centralDirSize);
        w.Write(centralDirOffset);
        w.Write((ushort)0);
    }

    private static uint Crc32(byte[] data)
    {
        const uint polynomial = 0xEDB88320;
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? polynomial ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }

        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
