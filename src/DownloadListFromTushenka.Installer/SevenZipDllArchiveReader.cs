using System.Reflection;
using SevenZipExtractor;

namespace DownloadListFromTushenka;

/// <summary>
/// Читает .7z через нативную библиотеку 7z.dll (пакет SevenZipExtractor) —
/// в отличие от управляемого LZMA-декодера, распаковывает большие
/// solid-архивы (несколько гигабайт) за разумное время.
///
/// 7z.dll вшита в сборку как embedded resource (см. csproj), а не лежит
/// рядом с exe отдельным файлом — так single-file публикация остаётся
/// действительно одним файлом, без сопутствующей папки x64/. Но
/// SevenZipExtractor грузит библиотеку через настоящий Win32 LoadLibrary
/// по пути на диске — из бандла напрямую так не получится, поэтому при
/// первом обращении содержимое ресурса один раз распаковывается во
/// временный файл, и уже его путь передаётся дальше.
/// </summary>
public sealed class SevenZipDllArchiveReader : IArchiveReader
{
    private const string ResourceName = "DownloadListFromTushenka.7z.dll";

    private static readonly string LibraryPath = ExtractLibraryToTempFile();

    private static string ExtractLibraryToTempFile()
    {
        var targetDir = Path.Combine(Path.GetTempPath(), "DownloadListFromTushenka-native");
        Directory.CreateDirectory(targetDir);
        var targetPath = Path.Combine(targetDir, "7z.dll");

        using var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' not found.");

        if (File.Exists(targetPath) && new FileInfo(targetPath).Length == resourceStream.Length)
        {
            return targetPath;
        }

        using var fileStream = File.Create(targetPath);
        resourceStream.CopyTo(fileStream);
        return targetPath;
    }

    public IReadOnlyList<string> ListEntryPaths(string archivePath)
    {
        using var archive = new ArchiveFile(archivePath, LibraryPath);
        return archive.Entries
            .Where(e => !e.IsFolder)
            .Select(e => e.FileName.Replace('/', Path.DirectorySeparatorChar))
            .ToList();
    }

    public ArchiveExtractResult ExtractTo(
        string archivePath, string destinationDirectory, bool overwriteExisting, Func<string, bool>? shouldSkipEntry = null)
    {
        var written = 0;
        var skipped = 0;
        var docsSkipped = 0;

        using var archive = new ArchiveFile(archivePath, LibraryPath);
        archive.Extract(entry =>
        {
            if (entry.IsFolder)
            {
                return null;
            }

            if (shouldSkipEntry?.Invoke(entry.FileName) == true)
            {
                docsSkipped++;
                return null;
            }

            var targetPath = ArchiveEntryPathResolver.Resolve(
                entry.FileName, destinationDirectory, overwriteExisting, File.Exists);
            if (targetPath is null)
            {
                skipped++;
                return null;
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            written++;
            return targetPath;
        });

        return new ArchiveExtractResult(written, skipped, docsSkipped);
    }
}
