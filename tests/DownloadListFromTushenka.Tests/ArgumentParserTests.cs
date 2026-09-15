using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class ArgumentParserTests
{
    [Fact]
    public void Parse_ReturnsNull_WhenNoArgs()
    {
        Assert.Null(ArgumentParser.Parse(Array.Empty<string>()));
    }

    [Fact]
    public void Parse_UsesDefaultOutputDirectory_WhenOutNotProvided()
    {
        var result = ArgumentParser.Parse(new[] { "https://sp-mod.com/list/1/a/b" });

        Assert.NotNull(result);
        Assert.Equal("https://sp-mod.com/list/1/a/b", result!.ListUrl);
        Assert.EndsWith("Download", result.OutputDirectory);
    }

    [Fact]
    public void Parse_UsesCustomOutputDirectory_WhenOutProvided()
    {
        var result = ArgumentParser.Parse(new[] { "https://sp-mod.com/list/1/a/b", "--out", "C:\\Mods" });

        Assert.NotNull(result);
        Assert.Equal("C:\\Mods", result!.OutputDirectory);
    }

    [Fact]
    public void Parse_ReturnsNull_WhenOutFlagMissingValue()
    {
        Assert.Null(ArgumentParser.Parse(new[] { "https://sp-mod.com/list/1/a/b", "--out" }));
    }

    [Fact]
    public void Parse_ReturnsNull_WhenUnknownFlagProvided()
    {
        Assert.Null(ArgumentParser.Parse(new[] { "https://sp-mod.com/list/1/a/b", "--bogus" }));
    }
}
