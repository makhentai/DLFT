using System.Text;
using DownloadListFromTushenka;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var sourceDir = InstallerPrompt.ReadRequiredPath(
    Console.In, Console.Out, "Путь к папке со скачанными модами: ");
if (sourceDir is null)
{
    Console.Error.WriteLine("Ввод прерван.");
    return PauseBeforeExit(1);
}

var destinationDir = InstallerPrompt.ReadRequiredPath(
    Console.In, Console.Out, "Путь к папке с игрой (SPT): ");
if (destinationDir is null)
{
    Console.Error.WriteLine("Ввод прерван.");
    return PauseBeforeExit(1);
}

Console.WriteLine();

var archiveReader = new CompositeArchiveReader(new ZipArchiveReader(), new SevenZipDllArchiveReader());
var installer = new ModArchiveInstaller(archiveReader);
var summary = installer.Install(
    sourceDir,
    destinationDir,
    conflictCount => InstallerPrompt.ReadOverwriteConfirmation(Console.In, Console.Out, conflictCount),
    Console.Out);

Console.WriteLine();
Console.WriteLine(
    $"Готово: установлено архивов {summary.ArchivesInstalled} из {summary.TotalArchives}, " +
    $"файлов записано {summary.FilesWritten}, пропущено {summary.FilesSkipped}.");

if (summary.Errors.Count > 0)
{
    Console.WriteLine("С ошибками:");
    foreach (var error in summary.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}

return PauseBeforeExit(summary.Errors.Count > 0 ? 2 : 0);

int PauseBeforeExit(int exitCode)
{
    Console.WriteLine();
    Console.Write("Нажмите Enter, чтобы закрыть...");
    Console.ReadLine();
    return exitCode;
}
