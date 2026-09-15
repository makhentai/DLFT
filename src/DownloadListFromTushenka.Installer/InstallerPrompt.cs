namespace DownloadListFromTushenka;

/// <summary>
/// Запрашивает у пользователя пути для установки и подтверждение
/// перезаписи конфликтующих файлов.
/// </summary>
public static class InstallerPrompt
{
    public static string? ReadRequiredPath(TextReader input, TextWriter output, string promptText)
    {
        while (true)
        {
            output.Write(promptText);
            var line = input.ReadLine();
            if (line is null)
            {
                return null;
            }

            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                return trimmed;
            }

            output.WriteLine("Путь не указан.");
        }
    }

    public static bool ReadOverwriteConfirmation(TextReader input, TextWriter output, int conflictCount)
    {
        output.WriteLine($"В папке назначения уже есть файлов, которые будут заменены: {conflictCount}.");
        output.Write("Перезаписать их? (y/n, по умолчанию n): ");

        var answer = input.ReadLine()?.Trim().ToLowerInvariant();
        return answer is "y" or "yes" or "д" or "да";
    }
}
