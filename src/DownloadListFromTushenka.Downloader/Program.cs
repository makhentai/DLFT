using System.Text;
using DownloadListFromTushenka;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var cliArgs = ArgumentParser.Parse(args);
if (cliArgs is not null)
{
    var (cliExitCode, _) = await RunDownloadAsync(cliArgs);
    return cliExitCode;
}

Console.WriteLine("Использование: DownloadListFromTushenka.Downloader.exe <ссылка-на-список> [--out <папка>]");
Console.WriteLine("Аргументы не переданы — введите данные вручную.");
Console.WriteLine();

while (true)
{
    var interactiveArgs = DownloaderPrompt.Read(Console.In, Console.Out);
    if (interactiveArgs is null)
    {
        Console.Error.WriteLine("Ввод прерван — ссылка на список не получена.");
        return PauseBeforeExit(1);
    }

    Console.WriteLine();
    var (exitCode, shouldRetry) = await RunDownloadAsync(interactiveArgs);
    if (!shouldRetry)
    {
        return PauseBeforeExit(exitCode);
    }

    Console.WriteLine("Проверьте ссылку и попробуйте снова.");
    Console.WriteLine();
}

int PauseBeforeExit(int exitCode)
{
    Console.WriteLine();
    Console.Write("Нажмите Enter, чтобы закрыть...");
    Console.ReadLine();
    return exitCode;
}

async Task<(int ExitCode, bool ShouldRetry)> RunDownloadAsync(ParsedArgs parsedArgs)
{
    const string userAgent = "DownloadListFromTushenka/1.0 (mod list downloader)";

    using var pageAndApiHttpClient = new HttpClient
    {
        BaseAddress = new Uri("https://sp-mod.com"),
        Timeout = TimeSpan.FromSeconds(30)
    };
    pageAndApiHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

    using var downloadHttpClient = new HttpClient
    {
        Timeout = TimeSpan.FromMinutes(5)
    };
    downloadHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

    Console.WriteLine($"Загружаю список: {parsedArgs.ListUrl}");

    string html;
    try
    {
        html = await pageAndApiHttpClient.GetStringAsync(parsedArgs.ListUrl);
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"Не удалось загрузить страницу списка: {ex.Message}");
        return (1, true);
    }
    catch (UriFormatException ex)
    {
        Console.Error.WriteLine($"Некорректная ссылка: {ex.Message}");
        return (1, true);
    }
    catch (TaskCanceledException)
    {
        Console.Error.WriteLine("Не удалось загрузить страницу списка: превышено время ожидания.");
        return (1, true);
    }

    var entries = ListHtmlParser.Parse(html);
    if (entries.Count == 0)
    {
        Console.Error.WriteLine("На странице не найдено ни одного мода/аддона. Проверьте, что это ссылка на список модов.");
        return (1, true);
    }

    Console.WriteLine($"Найдено записей: {entries.Count}");
    Console.WriteLine($"Папка вывода: {parsedArgs.OutputDirectory}");
    Console.WriteLine();

    var apiClient = new ForgeApiClient(pageAndApiHttpClient);
    var fileDownloader = new FileDownloader(downloadHttpClient);
    var orchestrator = new DownloadOrchestrator(apiClient, fileDownloader, Console.Out);

    var summary = await orchestrator.RunAsync(entries, parsedArgs.OutputDirectory, CancellationToken.None);

    Console.WriteLine();
    Console.WriteLine(
        $"Готово: скачано {summary.Downloaded}, пропущено {summary.Skipped}, ошибок {summary.Failed} из {summary.Total}.");

    if (summary.FailedItems.Count > 0)
    {
        Console.WriteLine("С ошибками:");
        foreach (var item in summary.FailedItems)
        {
            Console.WriteLine($"  - {item.Entry.Name} {item.Entry.Version}: {item.ErrorMessage}");
        }
    }

    return (summary.Failed > 0 ? 2 : 0, false);
}
