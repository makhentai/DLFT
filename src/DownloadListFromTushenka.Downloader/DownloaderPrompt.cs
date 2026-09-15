namespace DownloadListFromTushenka;

/// <summary>
/// Запрашивает у пользователя ссылку на список и папку для скачивания
/// построчно (используется при запуске exe без аргументов командной строки,
/// например двойным щелчком).
/// </summary>
public static class DownloaderPrompt
{
    public static ParsedArgs? Read(TextReader input, TextWriter output)
    {
        string listUrl;
        while (true)
        {
            output.Write("Ссылка на список модов: ");
            var line = input.ReadLine();
            if (line is null)
            {
                return null;
            }

            listUrl = line.Trim();
            if (!string.IsNullOrEmpty(listUrl))
            {
                break;
            }

            output.WriteLine("Ссылка не может быть пустой.");
        }

        var defaultOutputDirectory = Path.Combine(AppContext.BaseDirectory, "Download");
        output.Write($"Папка для скачивания (Enter — {defaultOutputDirectory}): ");
        var outputLine = input.ReadLine()?.Trim();
        var outputDirectory = string.IsNullOrEmpty(outputLine) ? defaultOutputDirectory : outputLine;

        return new ParsedArgs(listUrl, outputDirectory);
    }
}
