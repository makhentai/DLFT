using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DownloadListFromTushenka;

public sealed record ResolvedDownload(
    ModEntry Entry, string ResolvedVersion, string DownloadUrl, long? ContentLength, string? Warning);

public interface IForgeApiClient
{
    Task<ResolvedDownload?> ResolveDownloadAsync(ModEntry entry, CancellationToken cancellationToken);
}

public sealed class ForgeApiClient : IForgeApiClient
{
    private const int MaxAttempts = 4;

    // Проект собирается под SPT 4.1.3-4.1.5 — совместимой считаем любую
    // версию мода, чей spt_version_constraint пересекается с диапазоном
    // [4.1.0, 4.2.0).
    private static readonly Version TargetRangeMin = new(4, 1, 0);
    private static readonly Version TargetRangeMax = new(4, 2, 0);

    private readonly HttpClient _httpClient;
    private readonly Func<int, CancellationToken, Task> _delay;

    public ForgeApiClient(HttpClient httpClient, Func<int, CancellationToken, Task>? delay = null)
    {
        _httpClient = httpClient;
        _delay = delay ?? DefaultDelay;
    }

    public async Task<ResolvedDownload?> ResolveDownloadAsync(ModEntry entry, CancellationToken cancellationToken)
    {
        var kindSegment = entry.Kind == ModKind.Addon ? "addon" : "mod";

        var filtered = await FetchVersionsAsync(kindSegment, entry.Id, entry.Version, cancellationToken);
        var listed = filtered?.FirstOrDefault(v => v.Version == entry.Version && !string.IsNullOrEmpty(v.Link));

        if (listed is not null && IsCompatibleWithTarget(listed.SptVersionConstraint))
        {
            return new ResolvedDownload(entry, listed.Version, listed.Link!, listed.ContentLength, Warning: null);
        }

        // Версия, показанная в листе, не найдена или не подтверждена под
        // 4.1.x (например, список успел устареть) — смотрим всю историю
        // версий мода и берём новейшую, подходящую под наш SPT.
        var all = await FetchVersionsAsync(kindSegment, entry.Id, versionFilter: null, cancellationToken);
        var withLinks = (all ?? [])
            .Where(v => !string.IsNullOrEmpty(v.Link))
            .OrderByDescending(v => v.PublishedAt)
            .ToList();

        var best = withLinks.FirstOrDefault(v => IsCompatibleWithTarget(v.SptVersionConstraint));
        if (best is not null)
        {
            var warning = best.Version == entry.Version
                ? null
                : $"в листе указана версия {entry.Version}, скачана {best.Version} (подтверждена под SPT 4.1.x)";
            return new ResolvedDownload(entry, best.Version, best.Link!, best.ContentLength, warning);
        }

        var newest = withLinks.FirstOrDefault();
        if (newest is not null)
        {
            return new ResolvedDownload(
                entry, newest.Version, newest.Link!, newest.ContentLength,
                $"ни одна версия не подтверждена под SPT 4.1.x явно, скачана новейшая доступная ({newest.Version})");
        }

        if (listed is not null)
        {
            return new ResolvedDownload(
                entry, listed.Version, listed.Link!, listed.ContentLength,
                "версия из листа не подтверждена под SPT 4.1.x, другие версии получить не удалось");
        }

        return null;
    }

    private async Task<List<ForgeVersionDto>?> FetchVersionsAsync(
        string kindSegment, int id, string? versionFilter, CancellationToken cancellationToken)
    {
        var url = $"/api/v0/{kindSegment}/{id}/versions";
        if (versionFilter is not null)
        {
            url += $"?filter[version]={Uri.EscapeDataString(versionFilter)}";
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ForgeVersionsResponse>(url, cancellationToken);
                return response?.Data;
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts - 1)
            {
                await _delay(attempt, cancellationToken);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        return null;
    }

    public static bool IsCompatibleWithTarget(string? sptVersionConstraint)
    {
        var (min, max) = ParseConstraint(sptVersionConstraint);
        if (min is null && max is null)
        {
            // Ограничение неизвестного формата или отсутствует — считаем
            // неподтверждённым, а не совместимым.
            return false;
        }

        var effectiveMin = min ?? new Version(0, 0, 0);
        // Отсутствие верхней границы означает "не выше effectiveMin+эпсилон"
        // (голый номер версии = конкретный поддерживаемый релиз, а не "и выше").
        var effectiveMax = max ?? effectiveMin;

        return effectiveMin < TargetRangeMax && effectiveMax > TargetRangeMin;
    }

    private static readonly Regex RangePattern = new(
        @"^>=\s*([\d.]+)\s*<\s*([\d.]+)$", RegexOptions.Compiled);
    private static readonly Regex MinOnlyPattern = new(
        @"^>=\s*([\d.]+)$", RegexOptions.Compiled);

    public static (Version? Min, Version? Max) ParseConstraint(string? constraint)
    {
        if (string.IsNullOrWhiteSpace(constraint))
        {
            return (null, null);
        }

        constraint = constraint.Trim();

        if (constraint.StartsWith('~'))
        {
            var v = ParseVersionLoose(constraint[1..]);
            if (v is null) return (null, null);
            return (v, new Version(v.Major, v.Minor + 1, 0));
        }

        if (constraint.StartsWith('^'))
        {
            var v = ParseVersionLoose(constraint[1..]);
            if (v is null) return (null, null);
            return (v, new Version(v.Major + 1, 0, 0));
        }

        var rangeMatch = RangePattern.Match(constraint);
        if (rangeMatch.Success)
        {
            return (ParseVersionLoose(rangeMatch.Groups[1].Value), ParseVersionLoose(rangeMatch.Groups[2].Value));
        }

        var minOnlyMatch = MinOnlyPattern.Match(constraint);
        if (minOnlyMatch.Success)
        {
            return (ParseVersionLoose(minOnlyMatch.Groups[1].Value), null);
        }

        // Голый номер версии без оператора — трактуем как единственную
        // подтверждённую версию, а не как минимум "и выше".
        var bare = ParseVersionLoose(constraint);
        return bare is null ? (null, null) : (bare, bare);
    }

    private static Version? ParseVersionLoose(string s)
    {
        var cleaned = Regex.Match(s.Trim(), @"\d+(\.\d+){0,3}").Value;
        return Version.TryParse(cleaned, out var v) ? v : null;
    }

    private static Task DefaultDelay(int attempt, CancellationToken cancellationToken)
        => Task.Delay(TimeSpan.FromSeconds(2 * (attempt + 1)), cancellationToken);

    private sealed class ForgeVersionsResponse
    {
        [JsonPropertyName("data")]
        public List<ForgeVersionDto>? Data { get; set; }
    }

    private sealed class ForgeVersionDto
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("link")]
        public string? Link { get; set; }

        [JsonPropertyName("content_length")]
        public long? ContentLength { get; set; }

        [JsonPropertyName("spt_version_constraint")]
        public string? SptVersionConstraint { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }
    }
}
