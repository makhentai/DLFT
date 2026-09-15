namespace DownloadListFromTushenka;

public sealed record ArchiveExtractResult(int FilesWritten, int FilesSkipped, int DocsSkipped = 0);

/// <summary>
/// Абстракция над чтением архивов мода (.zip/.7z), чтобы
/// <see cref="ModArchiveInstaller"/> можно было тестировать без реальных
/// файлов на диске.
/// </summary>
public interface IArchiveReader
{
    /// <summary>
    /// Список относительных путей файлов внутри архива (без папок),
    /// с разделителями '/', нормализованный под текущую ОС не требуется —
    /// это делает вызывающая сторона.
    /// </summary>
    IReadOnlyList<string> ListEntryPaths(string archivePath);

    /// <summary>
    /// Распаковывает архив в <paramref name="destinationDirectory"/>,
    /// сохраняя внутреннюю структуру папок. Если файл по целевому пути уже
    /// существует и <paramref name="overwriteExisting"/> — false, запись
    /// этого файла пропускается (существующий файл не трогается).
    /// Если <paramref name="shouldSkipEntry"/> задан и возвращает true для
    /// относительного пути записи, она не распаковывается вовсе и
    /// учитывается в <see cref="ArchiveExtractResult.DocsSkipped"/>, а не
    /// в <see cref="ArchiveExtractResult.FilesSkipped"/>.
    /// </summary>
    ArchiveExtractResult ExtractTo(
        string archivePath,
        string destinationDirectory,
        bool overwriteExisting,
        Func<string, bool>? shouldSkipEntry = null);
}
