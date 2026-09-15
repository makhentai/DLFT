namespace DownloadListFromTushenka;

/// <summary>
/// Определяет "сопроводительные" файлы архива мода — readme, лицензии,
/// changelog и т.п. Многие авторы кладут их прямо в корень архива рядом с
/// BepInEx/SPT_Runtime; они не нужны для работы мода и по желанию
/// пользователя могут не распаковываться в папку игры.
/// </summary>
public static class DocFileFilter
{
    private static readonly HashSet<string> DocBaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "readme",
        "read me",
        "license",
        "licence",
        "eula",
        "changelog",
        "change log",
        "changes",
        "notice",
        "copying",
        "authors",
        "contributors",
        "credits",
    };

    /// <summary>
    /// true, если запись архива — это документационный файл (сравнение по
    /// имени без расширения и без учёта регистра/папки), а не файл самого
    /// мода.
    /// </summary>
    public static bool IsDocFile(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return DocBaseNames.Contains(withoutExtension);
    }
}
