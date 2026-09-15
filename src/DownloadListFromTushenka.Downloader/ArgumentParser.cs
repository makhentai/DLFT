namespace DownloadListFromTushenka;

public sealed record ParsedArgs(string ListUrl, string OutputDirectory);

public static class ArgumentParser
{
    public static ParsedArgs? Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        string? listUrl = null;
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "Download");

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--out")
            {
                if (i + 1 >= args.Length)
                {
                    return null;
                }
                outputDirectory = args[++i];
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            if (listUrl is not null)
            {
                return null;
            }

            listUrl = arg;
        }

        return listUrl is null ? null : new ParsedArgs(listUrl, outputDirectory);
    }
}
