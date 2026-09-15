using System.ComponentModel;

namespace DownloadListFromTushenka.App;

/// <summary>
/// Простой рантайм-переключатель языка интерфейса (RU/EN) без ResX и
/// сателлитных сборок — строки для XAML-биндингов вида
/// "{Binding Source={x:Static app:Loc.Instance}, Path=[Key]}" и для
/// код-behind (MessageBox и т.п.) через Loc.Instance["Key"].
///
/// Не переводит вывод общих библиотек (DownloadOrchestrator,
/// ModArchiveInstaller, ForgeApiClient) — они используются и консольными
/// Downloader/Installer, их лог остаётся русским независимо от этого
/// переключателя.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static readonly Loc Instance = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly Dictionary<string, (string Ru, string En)> Strings = new()
    {
        ["WindowTitle"] = ("Download List From Tushenka", "Download List From Tushenka"),
        ["HeaderTitle"] = ("DLFT — загрузчик и установщик модов для SPT 4.1", "DLFT — SPT 4.1 mod downloader and installer"),
        ["HeaderSubtitle"] = (
            "Скачай архивы модов по ссылке на лист, затем распакуй их в папку игры.",
            "Download mod archives from the list link, then extract them into the game folder."),

        ["TabInstructions"] = ("Инструкция", "Instructions"),
        ["TabDownload"] = ("Скачать моды", "Download mods"),
        ["TabInstall"] = ("Установить моды", "Install mods"),

        ["ThemeToggleToDark"] = ("Тёмная тема", "Dark theme"),
        ["ThemeToggleToLight"] = ("Светлая тема", "Light theme"),
        ["LanguageToggle"] = ("EN", "RU"),

        ["InstrWhatYouNeedHeader"] = ("Что понадобится", "What you'll need"),
        ["InstrWhatYouNeedBody"] = (
            "Чистая SPT 4.1, уже распакованная в папку игры — эта программа только скачивает и устанавливает моды, саму игру она не ставит.",
            "A clean SPT 4.1 install, already extracted into the game folder — this app only downloads and installs mods, it doesn't set up the game itself."),

        ["InstrStep2Header"] = ("Скачать моды", "Download mods"),
        ["InstrStep2Body"] = (
            "На вкладке «Скачать моды» вставь ссылку на лист модов sp-mod.com, выбери папку для загрузки и нажми «Скачать моды». Дождись, пока скачает всё — среди архивов есть довольно крупные.",
            "On the \"Download mods\" tab, paste the sp-mod.com list link, pick a download folder and click \"Download mods\". Wait for everything to finish — some archives are quite large."),

        ["InstrStep3Header"] = ("Установить моды", "Install mods"),
        ["InstrStep3Body"] = (
            "На вкладке «Установить моды» укажи папку со скачанными архивами и папку с игрой. Если не нужны readme/license/changelog файлов модов — включи галочку, чтобы они не разлетались по папке игры. Нажми «Установить моды». Программа сама разберёт архивы (.zip, .7z, .rar) и разложит файлы по нужным папкам. Если что-то уже стоит — один раз спросит, перезаписывать ли, и применит ответ ко всем совпадениям сразу.",
            "On the \"Install mods\" tab, point it at the folder with downloaded archives and the game folder. Check the box if you don't want mods' readme/license/changelog files scattered into the game folder. Click \"Install mods\" — it sorts out .zip/.7z/.rar archives itself and places files where they belong. If something already exists, it asks once whether to overwrite and applies that answer to every match."),

        ["ListUrlLabel"] = ("Ссылка на лист модов (sp-mod.com)", "Mod list link (sp-mod.com)"),
        ["DownloadFolderLabel"] = ("Папка для скачивания", "Download folder"),
        ["BrowseButton"] = ("Обзор…", "Browse…"),
        ["StartDownloadButton"] = ("Скачать моды", "Download mods"),
        ["LogLabel"] = ("Журнал", "Log"),

        ["ModsFolderLabel"] = ("Папка со скачанными архивами модов", "Folder with downloaded mod archives"),
        ["GameFolderLabel"] = ("Папка с игрой (SPT)", "Game folder (SPT)"),
        ["SkipDocFilesLabel"] = (
            "Не распаковывать readme, license, changelog и подобные файлы",
            "Don't extract readme, license, changelog and similar files"),
        ["StartInstallButton"] = ("Установить моды", "Install mods"),

        ["MissingDataTitle"] = ("Не хватает данных", "Missing information"),
        ["MissingListUrl"] = ("Укажи ссылку на лист модов.", "Enter the mod list link."),
        ["MissingDownloadFolder"] = ("Укажи папку для скачивания.", "Choose a download folder."),
        ["MissingBothFolders"] = (
            "Укажи обе папки — со скачанными модами и с игрой.",
            "Choose both folders — the downloaded mods folder and the game folder."),

        ["ErrorTitle"] = ("Ошибка", "Error"),
        ["ErrorLoadingList"] = ("Не удалось загрузить страницу списка: {0}", "Couldn't load the list page: {0}"),
        ["EmptyTitle"] = ("Пусто", "Nothing found"),
        ["EmptyList"] = (
            "На странице не найдено ни одного мода/аддона. Проверь, что это ссылка на список модов.",
            "No mods or addons found on that page. Check that it's really a link to a mod list."),

        ["ConflictTitle"] = ("Конфликт файлов", "File conflict"),
        ["ConflictBody"] = (
            "Найдено {0} файлов, которые уже есть в папке игры. Перезаписать их?",
            "Found {0} files that already exist in the game folder. Overwrite them?"),

        ["ChooseFolderDialogTitle"] = ("Выбери папку", "Choose a folder"),
        ["LoadingListStatus"] = ("Загружаю список модов...", "Loading the mod list..."),
        ["ErrorLoadingListStatus"] = ("Ошибка загрузки страницы списка.", "Failed to load the list page."),
        ["EmptyListStatus"] = ("В списке не найдено ни одного мода.", "No mods found in the list."),
        ["FoundEntriesDownloading"] = (
            "Найдено {0} записей. Скачиваю в {1}...",
            "Found {0} entries. Downloading into {1}..."),
        ["DownloadDoneStatus"] = (
            "Готово: скачано {0}, пропущено {1}, ошибок {2} из {3}.",
            "Done: downloaded {0}, skipped {1}, failed {2} of {3}."),
        ["InstallingStatus"] = ("Устанавливаю...", "Installing..."),
        ["InstallDoneStatus"] = (
            "Готово: установлено архивов {0} из {1}, файлов записано {2}, пропущено {3}{4}.",
            "Done: installed {0} of {1} archives, {2} files written, {3} skipped{4}."),
        ["DocsSkippedSuffix"] = (", документации не распаковано {0}", ", {0} doc files skipped"),

        ["FooterCredit"] = ("Часть проекта S&M · автор", "Part of the S&M project · by"),

        ["ViewErrorLogButton"] = ("Открыть журнал ошибок ({0})", "View error log ({0})"),
    };

    public string CurrentLanguage { get; private set; } = "ru";

    public void SetLanguage(string language)
    {
        if (CurrentLanguage == language)
        {
            return;
        }
        CurrentLanguage = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public void Toggle() => SetLanguage(CurrentLanguage == "ru" ? "en" : "ru");

    public string this[string key] =>
        Strings.TryGetValue(key, out var pair) ? (CurrentLanguage == "en" ? pair.En : pair.Ru) : key;
}
