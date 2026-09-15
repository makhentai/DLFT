namespace DownloadListFromTushenka;

public enum ModKind
{
    Mod,
    Addon
}

public sealed record ModEntry(ModKind Kind, int Id, string Slug, string Name, string Version);
