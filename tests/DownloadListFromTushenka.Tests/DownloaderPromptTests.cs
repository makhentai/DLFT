using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class DownloaderPromptTests
{
    [Fact]
    public void Read_ReturnsParsedArgs_FromTwoLines()
    {
        var input = new StringReader("https://sp-mod.com/list/1/a/b\nC:\\Mods\n");
        var output = new StringWriter();

        var result = DownloaderPrompt.Read(input, output);

        Assert.NotNull(result);
        Assert.Equal("https://sp-mod.com/list/1/a/b", result!.ListUrl);
        Assert.Equal("C:\\Mods", result.OutputDirectory);
    }

    [Fact]
    public void Read_UsesDefaultOutputDirectory_WhenSecondLineEmpty()
    {
        var input = new StringReader("https://sp-mod.com/list/1/a/b\n\n");
        var output = new StringWriter();

        var result = DownloaderPrompt.Read(input, output);

        Assert.NotNull(result);
        Assert.Equal("https://sp-mod.com/list/1/a/b", result!.ListUrl);
        Assert.EndsWith("Download", result.OutputDirectory);
    }

    [Fact]
    public void Read_RepromptsForListUrl_WhenFirstLinesAreEmpty()
    {
        var input = new StringReader("\n   \nhttps://sp-mod.com/list/1/a/b\n\n");
        var output = new StringWriter();

        var result = DownloaderPrompt.Read(input, output);

        Assert.NotNull(result);
        Assert.Equal("https://sp-mod.com/list/1/a/b", result!.ListUrl);
    }

    [Fact]
    public void Read_ReturnsNull_WhenInputEndsBeforeListUrlProvided()
    {
        var input = new StringReader(string.Empty);
        var output = new StringWriter();

        var result = DownloaderPrompt.Read(input, output);

        Assert.Null(result);
    }
}
