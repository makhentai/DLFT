namespace DownloadListFromTushenka;

/// <summary>
/// Выбирает конкретную реализацию <see cref="IArchiveReader"/>: сперва по
/// сигнатуре байтов в начале файла (<see cref="ArchiveFormatSniffer"/>),
/// и только если содержимое не распознано — по расширению файла.
/// Приоритет содержимого нужен потому, что некоторые моды публикуются с
/// "неправильным" расширением (например, обычный .zip с именем .7z) —
/// доверять расширению в таких случаях нельзя.
///
/// .rar обрабатывается тем же читателем, что и .7z: 7z.dll (пакет
/// SevenZipExtractor) умеет читать RAR (включая RAR5) на распаковку,
/// своего отдельного читателя не нужно.
/// </summary>
public sealed class CompositeArchiveReader : IArchiveReader
{
    private readonly IArchiveReader _zipReader;
    private readonly IArchiveReader _sevenZipReader;

    public CompositeArchiveReader(IArchiveReader zipReader, IArchiveReader sevenZipReader)
    {
        _zipReader = zipReader;
        _sevenZipReader = sevenZipReader;
    }

    public IReadOnlyList<string> ListEntryPaths(string archivePath)
        => Select(archivePath).ListEntryPaths(archivePath);

    public ArchiveExtractResult ExtractTo(
        string archivePath, string destinationDirectory, bool overwriteExisting, Func<string, bool>? shouldSkipEntry = null)
        => Select(archivePath).ExtractTo(archivePath, destinationDirectory, overwriteExisting, shouldSkipEntry);

    private IArchiveReader Select(string archivePath)
    {
        switch (ArchiveFormatSniffer.DetectFromFile(archivePath))
        {
            case DetectedArchiveFormat.Zip:
                return _zipReader;
            case DetectedArchiveFormat.SevenZip:
            case DetectedArchiveFormat.Rar:
                return _sevenZipReader;
        }

        var extension = Path.GetExtension(archivePath);
        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return _zipReader;
        }

        if (string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".rar", StringComparison.OrdinalIgnoreCase))
        {
            return _sevenZipReader;
        }

        throw new NotSupportedException($"Неподдерживаемый формат архива: {archivePath}");
    }
}
