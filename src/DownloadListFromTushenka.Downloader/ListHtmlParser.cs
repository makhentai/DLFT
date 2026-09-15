using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace DownloadListFromTushenka;

public static class ListHtmlParser
{
    private static readonly Regex EntryLinkPattern = new(
        @"^https://sp-mod\.com/(mod|addon)/(\d+)/([a-z0-9-]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VersionPattern = new(
        @"^\d+(\.\d+)+",
        RegexOptions.Compiled);

    public static IReadOnlyList<ModEntry> Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var entries = new List<ModEntry>();
        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors is null)
        {
            return entries;
        }

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", string.Empty);
            var match = EntryLinkPattern.Match(href);
            if (!match.Success)
            {
                continue;
            }

            var versionSpan = FindVersionSibling(anchor);
            if (versionSpan is null)
            {
                // Это ссылка-миниатюра той же карточки (без версии рядом);
                // текстовая ссылка с версией будет найдена отдельно.
                continue;
            }

            var kind = string.Equals(match.Groups[1].Value, "addon", StringComparison.OrdinalIgnoreCase)
                ? ModKind.Addon
                : ModKind.Mod;
            var id = int.Parse(match.Groups[2].Value);
            var slug = match.Groups[3].Value;
            var name = HtmlEntity.DeEntitize(anchor.InnerText).Trim();
            var version = HtmlEntity.DeEntitize(versionSpan.InnerText).Trim();

            entries.Add(new ModEntry(kind, id, slug, name, version));
        }

        return entries;
    }

    private static HtmlNode? FindVersionSibling(HtmlNode anchor)
    {
        var parent = anchor.ParentNode;
        if (parent is null)
        {
            return null;
        }

        foreach (var sibling in parent.ChildNodes)
        {
            if (sibling.Name != "span")
            {
                continue;
            }

            var classAttr = sibling.GetAttributeValue("class", string.Empty);
            if (!classAttr.Contains("shrink-0") || !classAttr.Contains("text-gray-400"))
            {
                continue;
            }

            var text = HtmlEntity.DeEntitize(sibling.InnerText).Trim();
            if (VersionPattern.IsMatch(text))
            {
                return sibling;
            }
        }

        return null;
    }
}
