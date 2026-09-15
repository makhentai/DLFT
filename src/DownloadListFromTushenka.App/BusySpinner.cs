using System.Windows.Controls;
using System.Windows.Threading;

namespace DownloadListFromTushenka.App;

/// <summary>
/// Крутящийся спиннер, дописываемый к статусной строке во время долгой
/// операции. Прогресс-бар почти не двигается между завершениями
/// отдельных архивов (особенно на крупных файлах) — без этого кажется,
/// что программа зависла.
/// </summary>
public sealed class BusySpinner
{
    private static readonly char[] Frames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏".ToCharArray();

    private readonly TextBlock _target;
    private readonly DispatcherTimer _timer;
    private string _baseText = string.Empty;
    private int _frame;

    public BusySpinner(TextBlock target)
    {
        _target = target;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _timer.Tick += (_, _) =>
        {
            _target.Text = $"{_baseText} {Frames[_frame % Frames.Length]}";
            _frame++;
        };
    }

    public void Start(string baseText)
    {
        _baseText = baseText;
        _frame = 0;
        _timer.Start();
    }

    public void UpdateBaseText(string baseText) => _baseText = baseText;

    public void Stop(string finalText)
    {
        _timer.Stop();
        _target.Text = finalText;
    }
}
