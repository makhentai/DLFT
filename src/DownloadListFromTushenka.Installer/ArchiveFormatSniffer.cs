namespace DownloadListFromTushenka;

public enum DetectedArchiveFormat
{
    Unknown,
    Zip,
    SevenZip,
    Rar
}

/// <summary>
/// Определяет реальный формат архива по сигнатуре байтов в начале файла,
/// а не по расширению. Некоторые моды публикуются с "неправильным"
/// расширением (например, обычный .zip выложен под именем .7z) — в этом
/// случае доверять расширению нельзя, иначе выбранный распаковщик
/// откажется открывать файл.
/// </summary>
public static class ArchiveFormatSniffer
{
    private static readonly byte[] ZipSignature = { 0x50, 0x4B };
    private static readonly byte[] SevenZipSignature = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };

    // RAR 1.5-4.x: "Rar!" + 0x1A 0x07 0x00. RAR 5.0+: "Rar!" + 0x1A 0x07 0x01 0x00.
    // Оба варианта делят первые 6 байт, поэтому одной сигнатуры достаточно
    // на распознавание — версию уточнять не нужно, 7z.dll читает оба.
    private static readonly byte[] RarSignature = { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 };

    public static DetectedArchiveFormat Detect(Stream stream)
    {
        var buffer = new byte[RarSignature.Length];
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length && (read = stream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0)
        {
            totalRead += read;
        }

        if (totalRead >= ZipSignature.Length && buffer.AsSpan(0, ZipSignature.Length).SequenceEqual(ZipSignature))
        {
            return DetectedArchiveFormat.Zip;
        }

        if (totalRead >= SevenZipSignature.Length && buffer.AsSpan(0, SevenZipSignature.Length).SequenceEqual(SevenZipSignature))
        {
            return DetectedArchiveFormat.SevenZip;
        }

        if (totalRead >= RarSignature.Length && buffer.AsSpan(0, RarSignature.Length).SequenceEqual(RarSignature))
        {
            return DetectedArchiveFormat.Rar;
        }

        return DetectedArchiveFormat.Unknown;
    }

    public static DetectedArchiveFormat DetectFromFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Detect(stream);
        }
        catch (IOException)
        {
            return DetectedArchiveFormat.Unknown;
        }
    }
}
