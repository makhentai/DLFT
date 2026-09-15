namespace DownloadListFromTushenka;

public sealed record InstallSummary(
    int TotalArchives,
    int ArchivesInstalled,
    int FilesWritten,
    int FilesSkipped,
    IReadOnlyList<string> Errors,
    int DocsSkipped = 0);

/// <summary>
/// Находит архивы модов (.zip/.7z) в папке-источнике и распаковывает их в
/// папку с игрой. Если целевые файлы уже существуют, вопрос "перезаписать?"
/// задаётся один раз для всех конфликтов сразу (через <c>confirmOverwrite</c>),
/// а не по одному на файл.
/// </summary>
public sealed class ModArchiveInstaller
{
    private const int MaxExtractAttempts = 3;

    private static readonly string[] SupportedExtensions = { ".zip", ".7z", ".rar" };

    private readonly IArchiveReader _archiveReader;
    private readonly Action<int> _retryDelay;

    public ModArchiveInstaller(IArchiveReader archiveReader, Action<int>? retryDelay = null)
    {
        _archiveReader = archiveReader;
        _retryDelay = retryDelay ?? DefaultRetryDelay;
    }

    private static void DefaultRetryDelay(int attempt)
        => Thread.Sleep(TimeSpan.FromMilliseconds(300 * (attempt + 1)));

    public InstallSummary Install(
        string sourceDirectory,
        string destinationDirectory,
        Func<int, bool> confirmOverwrite,
        TextWriter output,
        bool skipDocFiles = false)
    {
        var shouldSkipEntry = skipDocFiles ? DocFileFilter.IsDocFile : (Func<string, bool>?)null;

        var archives = Directory.Exists(sourceDirectory)
            ? Directory.EnumerateFiles(sourceDirectory)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        if (archives.Count == 0)
        {
            output.WriteLine("В указанной папке не найдено архивов модов (.zip/.7z).");
            return new InstallSummary(0, 0, 0, 0, Array.Empty<string>());
        }

        Directory.CreateDirectory(destinationDirectory);

        // Список записей читаем один раз на архив — используется и для
        // подсчёта конфликтов, и чтобы показать размер архива перед
        // распаковкой (большие solid .7z могут распаковываться очень долго,
        // и без этой строки процесс выглядит зависшим). Архив, который не
        // удалось открыть (битый файл, неверный формат и т.п.), не должен
        // ронять всю установку — помечаем его как ошибку и просто
        // исключаем из дальнейшей обработки.
        var entryCounts = new Dictionary<string, int>();
        var brokenArchives = new Dictionary<string, string>();
        var conflicting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var archivePath in archives)
        {
            IReadOnlyList<string> entryPaths;
            try
            {
                entryPaths = _archiveReader.ListEntryPaths(archivePath);
            }
            catch (Exception ex)
            {
                brokenArchives[archivePath] = ex.Message;
                continue;
            }

            entryCounts[archivePath] = entryPaths.Count;

            foreach (var relativePath in entryPaths)
            {
                if (skipDocFiles && DocFileFilter.IsDocFile(relativePath))
                {
                    continue;
                }

                var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
                var targetPath = Path.Combine(destinationDirectory, normalized);
                if (File.Exists(targetPath))
                {
                    conflicting.Add(targetPath);
                }
            }
        }

        var conflictCount = conflicting.Count;
        var overwrite = conflictCount > 0 && confirmOverwrite(conflictCount);

        var archivesInstalled = 0;
        var filesWritten = 0;
        var filesSkipped = 0;
        var docsSkipped = 0;
        var errors = new List<string>();

        for (var i = 0; i < archives.Count; i++)
        {
            var archivePath = archives[i];
            var name = Path.GetFileName(archivePath);

            if (brokenArchives.TryGetValue(archivePath, out var listError))
            {
                errors.Add($"{name}: {listError}");
                output.WriteLine($"[{i + 1}/{archives.Count}] {name} ... ОШИБКА: {listError}");
                continue;
            }

            output.WriteLine($"[{i + 1}/{archives.Count}] {name} ({entryCounts[archivePath]} файлов) — распаковка...");

            // Запись файлов иногда кратковременно падает — например,
            // антивирус на мгновение блокирует только что созданный,
            // неподписанный .dll, пока сканирует его. Повторная попытка
            // почти всегда проходит успешно, поэтому пробуем несколько раз
            // прежде чем считать архив реально сломанным.
            ArchiveExtractResult? result = null;
            Exception? lastError = null;
            for (var attempt = 0; attempt < MaxExtractAttempts; attempt++)
            {
                try
                {
                    result = _archiveReader.ExtractTo(archivePath, destinationDirectory, overwrite, shouldSkipEntry);
                    lastError = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < MaxExtractAttempts - 1)
                    {
                        _retryDelay(attempt);
                    }
                }
            }

            if (result is null)
            {
                errors.Add($"{name}: {lastError!.Message}");
                output.WriteLine($"  ОШИБКА: {lastError.Message}");
                continue;
            }

            archivesInstalled++;
            filesWritten += result.FilesWritten;
            filesSkipped += result.FilesSkipped;
            docsSkipped += result.DocsSkipped;
            var docsSuffix = result.DocsSkipped > 0 ? $", документация не распакована ({result.DocsSkipped})" : "";
            output.WriteLine($"  готово: записано {result.FilesWritten}, пропущено {result.FilesSkipped}{docsSuffix}.");
        }

        return new InstallSummary(archives.Count, archivesInstalled, filesWritten, filesSkipped, errors, docsSkipped);
    }
}
