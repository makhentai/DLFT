using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Win32;
using DownloadListFromTushenka;

namespace DownloadListFromTushenka.App;

public partial class MainWindow : Window
{
    private const string UserAgent = "DownloadListFromTushenka/1.0 (mod list downloader)";
    private static Loc L => Loc.Instance;

    private readonly BusySpinner _downloadSpinner;
    private readonly BusySpinner _installSpinner;

    public MainWindow()
    {
        InitializeComponent();
        _downloadSpinner = new BusySpinner(DownloadStatusText);
        _installSpinner = new BusySpinner(InstallStatusText);
        UpdateThemeButtonLabel();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.Toggle();
        UpdateThemeButtonLabel();
    }

    private void UpdateThemeButtonLabel()
    {
        ThemeToggleButton.Content = ThemeManager.Current == ThemeManager.Light
            ? L["ThemeToggleToDark"]
            : L["ThemeToggleToLight"];
    }

    private void LanguageToggleButton_Click(object sender, RoutedEventArgs e)
    {
        L.Toggle();
        UpdateThemeButtonLabel();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ViewDownloadErrorLogButton_Click(object sender, RoutedEventArgs e)
        => OpenLogFile(ViewDownloadErrorLogButton.Tag as string);

    private void ViewInstallErrorLogButton_Click(object sender, RoutedEventArgs e)
        => OpenLogFile(ViewInstallErrorLogButton.Tag as string);

    private static void OpenLogFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>
    /// Складывает список ошибок запуска в logs/{prefix}-errors-*.log рядом с
    /// exe — только когда ошибки реально есть, папка создаётся по факту.
    /// </summary>
    private static string WriteErrorLogFile(string prefix, IEnumerable<string> errorLines)
    {
        var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(logsDir);
        var filePath = Path.Combine(logsDir, $"{prefix}-errors-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.WriteAllLines(filePath, errorLines, Encoding.UTF8);
        return filePath;
    }

    private void BrowseDownloadFolderButton_Click(object sender, RoutedEventArgs e)
        => BrowseForFolder(DownloadFolderBox);

    private void BrowseModsFolderButton_Click(object sender, RoutedEventArgs e)
        => BrowseForFolder(ModsFolderBox);

    private void BrowseGameFolderButton_Click(object sender, RoutedEventArgs e)
        => BrowseForFolder(GameFolderBox);

    private void BrowseForFolder(System.Windows.Controls.TextBox target)
    {
        var dialog = new OpenFolderDialog
        {
            Title = L["ChooseFolderDialogTitle"],
            InitialDirectory = Directory.Exists(target.Text) ? target.Text : AppContext.BaseDirectory,
        };
        if (dialog.ShowDialog(this) == true)
        {
            target.Text = dialog.FolderName;
        }
    }

    private async void StartDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        var listUrl = ListUrlBox.Text.Trim();
        var outputDirectory = DownloadFolderBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(listUrl))
        {
            MessageBox.Show(this, L["MissingListUrl"], L["MissingDataTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            MessageBox.Show(this, L["MissingDownloadFolder"], L["MissingDataTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DownloadLogBox.Document.Blocks.Clear();
        ViewDownloadErrorLogButton.Visibility = Visibility.Collapsed;
        _downloadSpinner.Start(L["LoadingListStatus"]);
        DownloadProgressBar.IsIndeterminate = true;
        StartDownloadButton.IsEnabled = false;
        MainTabs.IsEnabled = false;

        try
        {
            using var pageAndApiHttpClient = new HttpClient
            {
                BaseAddress = new Uri("https://sp-mod.com"),
                Timeout = TimeSpan.FromSeconds(30),
            };
            pageAndApiHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            using var downloadHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            downloadHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            string html;
            try
            {
                html = await pageAndApiHttpClient.GetStringAsync(listUrl);
            }
            catch (Exception ex) when (ex is HttpRequestException or UriFormatException or TaskCanceledException)
            {
                _downloadSpinner.Stop(L["ErrorLoadingListStatus"]);
                MessageBox.Show(this, string.Format(L["ErrorLoadingList"], ex.Message), L["ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var entries = ListHtmlParser.Parse(html);
            if (entries.Count == 0)
            {
                _downloadSpinner.Stop(L["EmptyListStatus"]);
                MessageBox.Show(this, L["EmptyList"], L["EmptyTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Maximum = entries.Count;
            DownloadProgressBar.Value = 0;
            _downloadSpinner.UpdateBaseText(string.Format(L["FoundEntriesDownloading"], entries.Count, outputDirectory));

            var logWriter = new GuiLogWriter(DownloadLogBox)
            {
                OnLineWritten = () => Dispatcher.Invoke(() => DownloadProgressBar.Value = Math.Min(DownloadProgressBar.Value + 1, DownloadProgressBar.Maximum)),
            };

            var apiClient = new ForgeApiClient(pageAndApiHttpClient);
            var fileDownloader = new FileDownloader(downloadHttpClient);
            var orchestrator = new DownloadOrchestrator(apiClient, fileDownloader, logWriter);

            var summary = await orchestrator.RunAsync(entries, outputDirectory, CancellationToken.None);

            _downloadSpinner.Stop(string.Format(
                L["DownloadDoneStatus"], summary.Downloaded, summary.Skipped, summary.Failed, summary.Total));

            if (summary.FailedItems.Count > 0)
            {
                var errorDetails = summary.FailedItems
                    .Select(item => LogTranslator.ToEnglish($"{item.Entry.Name} {item.Entry.Version}: {item.ErrorMessage}"))
                    .ToList();

                logWriter.WriteLine("Errors:");
                foreach (var detail in errorDetails)
                {
                    logWriter.WriteLine($"  - {detail}");
                }

                var logPath = WriteErrorLogFile("download", errorDetails);
                ViewDownloadErrorLogButton.Content = string.Format(L["ViewErrorLogButton"], errorDetails.Count);
                ViewDownloadErrorLogButton.Tag = logPath;
                ViewDownloadErrorLogButton.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _downloadSpinner.Stop(DownloadStatusText.Text);
            StartDownloadButton.IsEnabled = true;
            MainTabs.IsEnabled = true;
        }
    }

    private async void StartInstallButton_Click(object sender, RoutedEventArgs e)
    {
        var sourceDirectory = ModsFolderBox.Text.Trim();
        var destinationDirectory = GameFolderBox.Text.Trim();
        var skipDocFiles = SkipDocFilesCheckBox.IsChecked == true;

        if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(destinationDirectory))
        {
            MessageBox.Show(this, L["MissingBothFolders"], L["MissingDataTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        InstallLogBox.Document.Blocks.Clear();
        ViewInstallErrorLogButton.Visibility = Visibility.Collapsed;
        _installSpinner.Start(L["InstallingStatus"]);
        InstallProgressBar.IsIndeterminate = true;
        StartInstallButton.IsEnabled = false;
        MainTabs.IsEnabled = false;

        try
        {
            var logWriter = new GuiLogWriter(InstallLogBox);
            var archiveReader = new CompositeArchiveReader(new ZipArchiveReader(), new SevenZipDllArchiveReader());
            var installer = new ModArchiveInstaller(archiveReader);

            var summary = await Task.Run(() => installer.Install(
                sourceDirectory,
                destinationDirectory,
                conflictCount => Dispatcher.Invoke(() => MessageBox.Show(
                    this,
                    string.Format(L["ConflictBody"], conflictCount),
                    L["ConflictTitle"],
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes),
                logWriter,
                skipDocFiles));

            var docsSuffix = summary.DocsSkipped > 0 ? string.Format(L["DocsSkippedSuffix"], summary.DocsSkipped) : "";
            _installSpinner.Stop(string.Format(
                L["InstallDoneStatus"], summary.ArchivesInstalled, summary.TotalArchives, summary.FilesWritten, summary.FilesSkipped, docsSuffix));

            if (summary.Errors.Count > 0)
            {
                var errorDetails = summary.Errors.Select(LogTranslator.ToEnglish).ToList();

                logWriter.WriteLine("Errors:");
                foreach (var detail in errorDetails)
                {
                    logWriter.WriteLine($"  - {detail}");
                }

                var logPath = WriteErrorLogFile("install", errorDetails);
                ViewInstallErrorLogButton.Content = string.Format(L["ViewErrorLogButton"], errorDetails.Count);
                ViewInstallErrorLogButton.Tag = logPath;
                ViewInstallErrorLogButton.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _installSpinner.Stop(InstallStatusText.Text);
            InstallProgressBar.IsIndeterminate = false;
            StartInstallButton.IsEnabled = true;
            MainTabs.IsEnabled = true;
        }
    }
}
