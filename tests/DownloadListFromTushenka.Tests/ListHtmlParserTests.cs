using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class ListHtmlParserTests
{
    private static string LoadFixture()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "list-page-sample.html"));

    [Fact]
    public void Parse_ExtractsAllEntries_WithCorrectKindIdSlugNameVersion()
    {
        var html = LoadFixture();

        var entries = ListHtmlParser.Parse(html);

        Assert.Equal(3, entries.Count);

        Assert.Equal(ModKind.Mod, entries[0].Kind);
        Assert.Equal(2905, entries[0].Id);
        Assert.Equal("ragdoll-kinetics", entries[0].Slug);
        Assert.Equal("Ragdoll Kinetics", entries[0].Name);
        Assert.Equal("1.2.0", entries[0].Version);

        Assert.Equal(ModKind.Mod, entries[1].Kind);
        Assert.Equal(2888, entries[1].Id);
        Assert.Equal("task-item-indicator", entries[1].Slug);
        Assert.Equal("Task Item Indicator", entries[1].Name);
        Assert.Equal("1.0.0", entries[1].Version);

        Assert.Equal(ModKind.Addon, entries[2].Kind);
        Assert.Equal(39, entries[2].Id);
        Assert.Equal("climbable-ladders-fika-sync", entries[2].Slug);
        Assert.Equal("Climbable Ladders - Fika sync", entries[2].Name);
        Assert.Equal("1.0.1", entries[2].Version);
    }

    [Fact]
    public void Parse_ReturnsEmptyList_WhenNoEntriesFound()
    {
        var entries = ListHtmlParser.Parse("<html><body><p>ничего интересного</p></body></html>");

        Assert.Empty(entries);
    }
}
