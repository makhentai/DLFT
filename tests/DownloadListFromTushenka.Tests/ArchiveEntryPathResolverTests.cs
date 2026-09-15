using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class ArchiveEntryPathResolverTests
{
    private static readonly string DestinationRoot = OperatingSystem.IsWindows()
        ? @"C:\Games\SPT"
        : "/games/spt";

    [Fact]
    public void Resolve_ReturnsTargetPath_ForNormalEntry()
    {
        var target = ArchiveEntryPathResolver.Resolve(
            "BepInEx/plugins/Mod.dll", DestinationRoot, overwriteExisting: false, _ => false);

        Assert.NotNull(target);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(DestinationRoot, "BepInEx", "plugins", "Mod.dll")),
            target);
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("../../evil.txt")]
    [InlineData("BepInEx/../../evil.txt")]
    public void Resolve_ReturnsNull_ForZipSlipEntry(string entryKey)
    {
        var target = ArchiveEntryPathResolver.Resolve(
            entryKey, DestinationRoot, overwriteExisting: true, _ => false);

        Assert.Null(target);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenFileExistsAndOverwriteIsFalse()
    {
        var target = ArchiveEntryPathResolver.Resolve(
            "Mod.dll", DestinationRoot, overwriteExisting: false, _ => true);

        Assert.Null(target);
    }

    [Fact]
    public void Resolve_ReturnsTargetPath_WhenFileExistsAndOverwriteIsTrue()
    {
        var target = ArchiveEntryPathResolver.Resolve(
            "Mod.dll", DestinationRoot, overwriteExisting: true, _ => true);

        Assert.NotNull(target);
    }
}
