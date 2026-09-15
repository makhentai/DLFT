using System.Windows;

namespace DownloadListFromTushenka.App;

/// <summary>
/// Переключает тёмную/светлую тему на лету — подменяет второй merged
/// dictionary в Application.Resources (первый — Styles.xaml, который
/// ссылается на кисти темы через DynamicResource и сам не меняется).
/// </summary>
public static class ThemeManager
{
    public const string Light = "light";
    public const string Dark = "dark";

    public static string Current { get; private set; } = Light;

    public static void Apply(string theme)
    {
        Current = theme;
        var uri = new Uri($"Themes/{(theme == Dark ? "Dark" : "Light")}.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        var merged = Application.Current.Resources.MergedDictionaries;
        // Styles.xaml остаётся первым — тема всегда второй элемент.
        if (merged.Count > 1)
        {
            merged[1] = dict;
        }
        else
        {
            merged.Add(dict);
        }
    }

    public static void Toggle() => Apply(Current == Light ? Dark : Light);
}
