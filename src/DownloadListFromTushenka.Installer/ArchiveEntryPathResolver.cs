namespace DownloadListFromTushenka;

/// <summary>
/// Общая логика вычисления безопасного целевого пути для записи архива —
/// используется всеми реализациями <see cref="IArchiveReader"/>. Чистая
/// функция (без обращений к реальному архиву), поэтому легко тестируется.
/// </summary>
public static class ArchiveEntryPathResolver
{
    /// <summary>
    /// Возвращает полный целевой путь для записи, либо null, если запись
    /// нужно пропустить — либо потому что она пытается выйти за пределы
    /// <paramref name="destinationRoot"/> (защита от zip slip), либо потому
    /// что файл уже существует, а перезапись не разрешена.
    /// </summary>
    public static string? Resolve(
        string entryKey,
        string destinationRoot,
        bool overwriteExisting,
        Func<string, bool> fileExists)
    {
        var relativePath = entryKey.Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(destinationRoot);
        var targetPath = Path.GetFullPath(Path.Combine(root, relativePath));

        var relativeToRoot = Path.GetRelativePath(root, targetPath);
        if (relativeToRoot.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relativeToRoot))
        {
            return null;
        }

        if (fileExists(targetPath) && !overwriteExisting)
        {
            return null;
        }

        return targetPath;
    }
}
