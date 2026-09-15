using System.IO.Compression;
using System.Text;

namespace DownloadListFromTushenka;

/// <summary>
/// Читает .zip через встроенный System.IO.Compression — быстрый, без
/// внешних зависимостей.
/// </summary>
public sealed class ZipArchiveReader : IArchiveReader
{
    /// <summary>
    /// Кодировка для имён записей, у которых не выставлен ZIP-флаг UTF-8
    /// (бит 11 general purpose flag). .NET по умолчанию в этом случае берёт
    /// IBM437, что превращает кириллицу в мусор — большинство архивов на
    /// Windows без флага UTF-8 на деле кодируют имена в ANSI-кодировке
    /// системы (для русской Windows это cp1251), поэтому берём её как
    /// более разумный запасной вариант. Архивы, у которых флаг UTF-8
    /// выставлен (так делает 7-Zip и большинство современных архиваторов),
    /// декодируются как UTF-8 независимо от этого параметра — так работает
    /// System.IO.Compression.
    /// </summary>
    private static readonly Encoding LegacyEntryNameEncoding = CreateLegacyEncoding();

    private static Encoding CreateLegacyEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1251);
    }

    public IReadOnlyList<string> ListEntryPaths(string archivePath)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Read, LegacyEntryNameEncoding);
        return archive.Entries
            .Where(e => !IsDirectoryEntry(e))
            .Select(e => e.FullName.Replace('/', Path.DirectorySeparatorChar))
            .ToList();
    }

    public ArchiveExtractResult ExtractTo(
        string archivePath, string destinationDirectory, bool overwriteExisting, Func<string, bool>? shouldSkipEntry = null)
    {
        var written = 0;
        var skipped = 0;
        var docsSkipped = 0;

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Read, LegacyEntryNameEncoding);
        foreach (var entry in archive.Entries)
        {
            if (IsDirectoryEntry(entry))
            {
                continue;
            }

            if (shouldSkipEntry?.Invoke(entry.FullName) == true)
            {
                docsSkipped++;
                continue;
            }

            var targetPath = ArchiveEntryPathResolver.Resolve(
                entry.FullName, destinationDirectory, overwriteExisting, File.Exists);
            if (targetPath is null)
            {
                skipped++;
                continue;
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            entry.ExtractToFile(targetPath, overwrite: true);
            written++;
        }

        return new ArchiveExtractResult(written, skipped, docsSkipped);
    }

    private static bool IsDirectoryEntry(ZipArchiveEntry entry)
        => string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/');
}
