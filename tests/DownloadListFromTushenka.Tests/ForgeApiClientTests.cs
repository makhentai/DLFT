using System.Net;
using System.Text;
using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class ForgeApiClientTests
{
    [Fact]
    public async Task ResolveDownloadAsync_ReturnsLinkAndContentLength_WhenVersionMatches()
    {
        const string json = """
        {
          "success": true,
          "data": [
            {
              "id": 14730,
              "version": "1.2.0",
              "link": "https://sp-mod.com/mod/download/2905/ragdoll-kinetics/1.2.0",
              "content_length": 45393,
              "spt_version_constraint": "~4.1.5",
              "published_at": "2026-09-01T00:00:00.000000Z"
            }
          ]
        }
        """;

        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sp-mod.com") };
        var client = new ForgeApiClient(httpClient);
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "1.2.0");

        var result = await client.ResolveDownloadAsync(entry, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("https://sp-mod.com/mod/download/2905/ragdoll-kinetics/1.2.0", result!.DownloadUrl);
        Assert.Equal("1.2.0", result.ResolvedVersion);
        Assert.Null(result.Warning);
        Assert.Equal(45393, result.ContentLength);
        Assert.Equal("/api/v0/mod/2905/versions", capturedRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("1.2.0", capturedRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task ResolveDownloadAsync_UsesAddonPath_ForAddonEntries()
    {
        const string json = """{ "success": true, "data": [ { "id": 1, "version": "1.0.1", "link": "https://sp-mod.com/addon/download/39/climbable-ladders-fika-sync/1.0.1", "content_length": 1024, "spt_version_constraint": "~4.1.5", "published_at": "2026-09-01T00:00:00.000000Z" } ] }""";

        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sp-mod.com") };
        var client = new ForgeApiClient(httpClient);
        var entry = new ModEntry(ModKind.Addon, 39, "climbable-ladders-fika-sync", "Climbable Ladders - Fika sync", "1.0.1");

        var result = await client.ResolveDownloadAsync(entry, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("/api/v0/addon/39/versions", capturedRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ResolveDownloadAsync_ReturnsNull_WhenVersionNotInResponse()
    {
        const string json = """{ "success": true, "data": [] }""";
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sp-mod.com") };
        var client = new ForgeApiClient(httpClient);
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "9.9.9");

        var result = await client.ResolveDownloadAsync(entry, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ResolveDownloadAsync_RetriesOnTooManyRequests_ThenSucceeds()
    {
        var callCount = 0;
        const string json = """{ "success": true, "data": [ { "id": 1, "version": "1.0.0", "link": "https://sp-mod.com/mod/download/1/some-mod/1.0.0", "content_length": 10, "spt_version_constraint": "~4.1.5", "published_at": "2026-09-01T00:00:00.000000Z" } ] }""";
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return callCount < 3
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sp-mod.com") };
        var client = new ForgeApiClient(httpClient, delay: (_, _) => Task.CompletedTask);
        var entry = new ModEntry(ModKind.Mod, 1, "some-mod", "Some Mod", "1.0.0");

        var result = await client.ResolveDownloadAsync(entry, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task ResolveDownloadAsync_FallsBackToNewerCompatibleVersion_WhenListedVersionTargetsOldSpt()
    {
        const string filteredJson = """
        {
          "success": true,
          "data": [
            {
              "id": 14324,
              "version": "0.3.1",
              "link": "https://sp-mod.com/mod/download/2866/manimals-icebreaker-backport/0.3.1",
              "content_length": 111,
              "spt_version_constraint": "~4.0.13",
              "published_at": "2026-08-20T06:27:00.000000Z"
            }
          ]
        }
        """;
        const string fullJson = """
        {
          "success": true,
          "data": [
            {
              "id": 14324,
              "version": "0.3.1",
              "link": "https://sp-mod.com/mod/download/2866/manimals-icebreaker-backport/0.3.1",
              "content_length": 111,
              "spt_version_constraint": "~4.0.13",
              "published_at": "2026-08-20T06:27:00.000000Z"
            },
            {
              "id": 15410,
              "version": "1.1.3",
              "link": "https://sp-mod.com/mod/download/2866/manimals-icebreaker-backport/1.1.3",
              "content_length": 222,
              "spt_version_constraint": "~4.1.5",
              "published_at": "2026-09-15T01:12:00.000000Z"
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(req =>
        {
            var isFiltered = req.RequestUri!.Query.Contains("filter");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(isFiltered ? filteredJson : fullJson, Encoding.UTF8, "application/json")
            };
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sp-mod.com") };
        var client = new ForgeApiClient(httpClient);
        var entry = new ModEntry(ModKind.Mod, 2866, "manimals-icebreaker-backport", "Manimal's Icebreaker Backport", "0.3.1");

        var result = await client.ResolveDownloadAsync(entry, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("1.1.3", result!.ResolvedVersion);
        Assert.Equal("https://sp-mod.com/mod/download/2866/manimals-icebreaker-backport/1.1.3", result.DownloadUrl);
        Assert.NotNull(result.Warning);
        Assert.Contains("4.1.x", result.Warning);
    }

    [Theory]
    [InlineData("~4.1.5", true)]
    [InlineData("~4.0.0", false)]
    [InlineData(">=4.0.13 <4.1.0", false)]
    [InlineData(">=4.0.12 <4.1.0", false)]
    [InlineData("3.7.3", false)]
    [InlineData(null, false)]
    public void IsCompatibleWithTarget_EvaluatesSptVersionRangesAgainst41x(string? constraint, bool expected)
    {
        Assert.Equal(expected, ForgeApiClient.IsCompatibleWithTarget(constraint));
    }

    [Fact]
    public async Task ResolveDownloadAsync_ReturnsNull_WhenRateLimitedOnEveryAttempt()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://sp-mod.com") };
        var client = new ForgeApiClient(httpClient, delay: (_, _) => Task.CompletedTask);
        var entry = new ModEntry(ModKind.Mod, 1, "some-mod", "Some Mod", "1.0.0");

        var result = await client.ResolveDownloadAsync(entry, CancellationToken.None);

        Assert.Null(result);
    }
}
