using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace DownloadListFromTushenka.App;

/// <summary>
/// TextWriter, дописывающий каждую строку в RichTextBox лога отдельным
/// абзацем, с цветом по смыслу строки (ошибка/предупреждение/успех).
/// Оркестраторы (DownloadOrchestrator, ModArchiveInstaller) пишут строки
/// прогресса из фоновых потоков (продолжения async-задач, Task.Run) —
/// поэтому добавление в RichTextBox всегда идёт через Dispatcher.
///
/// Строки от общих библиотек (используются и консольными
/// Downloader/Installer) приходят по-русски — здесь переводятся на
/// английский под конкретный, заранее известный набор форматов, сами
/// библиотеки не трогаем: их продолжают использовать русские консольные
/// версии.
/// </summary>
public sealed class GuiLogWriter : TextWriter
{
    private readonly RichTextBox _target;
    private readonly Dispatcher _dispatcher;

    public Action? OnLineWritten { get; set; }

    public GuiLogWriter(RichTextBox target)
    {
        _target = target;
        _dispatcher = target.Dispatcher;
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void WriteLine(string? value)
    {
        var line = LogTranslator.ToEnglish(value ?? string.Empty);
        var resourceKey = ClassifyResourceKey(line);

        _dispatcher.Invoke(() =>
        {
            var run = new Run(line);
            if (resourceKey is not null)
            {
                run.SetResourceReference(TextElement.ForegroundProperty, resourceKey);
            }
            _target.Document.Blocks.Add(new Paragraph(run) { Margin = new Thickness(0, 0, 0, 2) });
            _target.ScrollToEnd();
        });
        OnLineWritten?.Invoke();
    }

    public override void Write(char value)
    {
        // Оркестраторы всегда пишут целыми строками через WriteLine —
        // посимвольная запись не используется, реализация не нужна.
    }

    private static string? ClassifyResourceKey(string line)
    {
        if (line.Contains("ERROR", StringComparison.Ordinal))
        {
            return "LogErrorBrush";
        }
        if (line.Contains("WARNING", StringComparison.Ordinal))
        {
            return "LogWarningBrush";
        }
        if (line.Contains(" OK", StringComparison.Ordinal) || line.Contains("done:", StringComparison.Ordinal))
        {
            return "LogSuccessBrush";
        }
        return null;
    }
}

internal static class LogTranslator
{
    public static string ToEnglish(string line)
    {
        // Простые фразы и статусы.
        line = line.Replace("уже скачан", "already downloaded");
        line = line.Replace("ОШИБКА:", "ERROR:");
        line = line.Replace("ВНИМАНИЕ:", "WARNING:");
        line = line.Replace("С ошибками:", "Errors:");
        line = line.Replace("версия не найдена в API", "version not found in API");
        line = line.Replace(
            "В указанной папке не найдено архивов модов (.zip/.7z).",
            "No mod archives found in the specified folder (.zip/.7z/.rar).");
        line = Regex.Replace(line, @"Неподдерживаемый формат архива: (.+)$", "Unsupported archive format: $1");

        // ForgeApiClient: предупреждения о подмене версии.
        line = Regex.Replace(
            line,
            @"в листе указана версия (\S+), скачана (\S+) \(подтверждена под SPT 4\.1\.x\)",
            "list showed version $1, downloaded $2 instead (confirmed for SPT 4.1.x)");
        line = Regex.Replace(
            line,
            @"ни одна версия не подтверждена под SPT 4\.1\.x явно, скачана новейшая доступная \(([^)]+)\)",
            "no version explicitly confirmed for SPT 4.1.x, downloaded the newest available ($1)");
        line = line.Replace(
            "версия из листа не подтверждена под SPT 4.1.x, другие версии получить не удалось",
            "the listed version isn't confirmed for SPT 4.1.x, and no other versions could be fetched");

        // ModArchiveInstaller: строки распаковки.
        line = Regex.Replace(line, @"\((\d+) файлов\) — распаковка\.\.\.", "($1 files) - extracting...");
        line = Regex.Replace(
            line,
            @"готово: записано (\d+), пропущено (\d+)",
            "done: $1 written, $2 skipped");
        line = Regex.Replace(
            line,
            @", документация не распакована \((\d+)\)",
            ", $1 doc files skipped");

        return line;
    }
}
