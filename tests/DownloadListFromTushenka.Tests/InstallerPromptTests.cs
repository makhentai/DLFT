using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class InstallerPromptTests
{
    [Fact]
    public void ReadRequiredPath_ReturnsTrimmedLine()
    {
        var input = new StringReader("  C:\\Mods  \n");
        var output = new StringWriter();

        var result = InstallerPrompt.ReadRequiredPath(input, output, "Путь: ");

        Assert.Equal("C:\\Mods", result);
    }

    [Fact]
    public void ReadRequiredPath_RepromptsWithMessage_WhenLineIsEmpty()
    {
        var input = new StringReader("\n   \nC:\\Mods\n");
        var output = new StringWriter();

        var result = InstallerPrompt.ReadRequiredPath(input, output, "Путь: ");

        Assert.Equal("C:\\Mods", result);
        Assert.Contains("Путь не указан", output.ToString());
    }

    [Fact]
    public void ReadRequiredPath_ReturnsNull_WhenInputEnds()
    {
        var input = new StringReader(string.Empty);
        var output = new StringWriter();

        var result = InstallerPrompt.ReadRequiredPath(input, output, "Путь: ");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("yes", true)]
    [InlineData("д", true)]
    [InlineData("да", true)]
    [InlineData("n", false)]
    [InlineData("no", false)]
    [InlineData("", false)]
    [InlineData("что угодно ещё", false)]
    public void ReadOverwriteConfirmation_ParsesAnswer(string answer, bool expected)
    {
        var input = new StringReader(answer + "\n");
        var output = new StringWriter();

        var result = InstallerPrompt.ReadOverwriteConfirmation(input, output, conflictCount: 3);

        Assert.Equal(expected, result);
    }
}
