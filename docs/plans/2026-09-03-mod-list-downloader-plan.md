# DownloadListFromTushenka Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Консольная self-contained утилита для Windows, которая по ссылке на список модов sp-mod.com скачивает все моды/аддоны списка в папку `Download`.

**Architecture:** Конвейер из четырёх независимых, тестируемых компонентов: `ListHtmlParser` (парсинг HTML списка → `ModEntry[]`), `ForgeApiClient` (резолв точной ссылки на файл через официальный API), `FileDownloader` (скачивание файла на диск, дедупликация), `DownloadOrchestrator` (координация с ограниченным параллелизмом, прогресс, сводка). `Program.cs` — тонкая обвязка CLI.

**Tech Stack:** .NET 8 (C#), HtmlAgilityPack (парсинг HTML), System.Text.Json (десериализация API), xUnit (тесты).

**Spec:** [docs/specs/2026-09-03-mod-list-downloader-design.md](../specs/2026-09-03-mod-list-downloader-design.md)

## Global Constraints

- Аутентификация не требуется — API и страницы сайта публично доступны.
- Сборка: .NET 8, публикуется как self-contained single-file exe для `win-x64`.
- Папка вывода по умолчанию: `Download/` рядом с exe; уже существующие файлы не перекачиваются.
- Параллелизм скачивания/резолва: не более 4 одновременных операций.
- Таймауты: 30 секунд на запрос страницы списка/API, 5 минут на скачивание одного файла.
- Ошибка по одной записи списка не должна прерывать обработку остальных.
- User-Agent запросов — описательный (не пустой/дефолтный), как просит API.

---

### Task 1: Каркас проекта + `ModEntry` + `ListHtmlParser`

**Files:**
- Create: `src/DownloadListFromTushenka/DownloadListFromTushenka.csproj`
- Create: `src/DownloadListFromTushenka/Program.cs` (временно — сгенерированный шаблон, будет переписан в Task 6)
- Create: `src/DownloadListFromTushenka/ModEntry.cs`
- Create: `src/DownloadListFromTushenka/ListHtmlParser.cs`
- Create: `tests/DownloadListFromTushenka.Tests/DownloadListFromTushenka.Tests.csproj`
- Create: `tests/DownloadListFromTushenka.Tests/ListHtmlParserTests.cs`
- Create: `tests/DownloadListFromTushenka.Tests/Fixtures/list-page-sample.html`
- Create: `DownloadListFromTushenka.sln`
- Create: `.gitignore`

**Interfaces:**
- Produces: `enum ModKind { Mod, Addon }`; `record ModEntry(ModKind Kind, int Id, string Slug, string Name, string Version)`; `static class ListHtmlParser { static IReadOnlyList<ModEntry> Parse(string html) }` — используется во всех последующих задачах.

- [ ] **Step 1: Инициализировать git-репозиторий и решение**

```bash
cd "D:/!AI/CLAUDE/DownloadListFromTushenka"
git init
dotnet new sln -n DownloadListFromTushenka
```

- [ ] **Step 2: Создать основной проект и проект тестов через dotnet CLI**

```bash
dotnet new console -o src/DownloadListFromTushenka -n DownloadListFromTushenka
dotnet new xunit -o tests/DownloadListFromTushenka.Tests -n DownloadListFromTushenka.Tests
dotnet sln add src/DownloadListFromTushenka/DownloadListFromTushenka.csproj
dotnet sln add tests/DownloadListFromTushenka.Tests/DownloadListFromTushenka.Tests.csproj
dotnet add tests/DownloadListFromTushenka.Tests reference src/DownloadListFromTushenka
dotnet add src/DownloadListFromTushenka package HtmlAgilityPack
rm tests/DownloadListFromTushenka.Tests/UnitTest1.cs
```

- [ ] **Step 3: Настроить `DownloadListFromTushenka.csproj` под self-contained single-file публикацию**

Открыть `src/DownloadListFromTushenka/DownloadListFromTushenka.csproj` и привести `<PropertyGroup>` к виду:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>DownloadListFromTushenka</RootNamespace>
    <AssemblyName>DownloadListFromTushenka</AssemblyName>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="HtmlAgilityPack" Version="1.11.63" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Настроить тестовый проект на копирование фикстур в output**

Добавить в `tests/DownloadListFromTushenka.Tests/DownloadListFromTushenka.Tests.csproj` перед закрывающим `</Project>`:

```xml
  <ItemGroup>
    <None Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
```

- [ ] **Step 5: Добавить `.gitignore`**

```
bin/
obj/
Download/
```

- [ ] **Step 6: Создать тестовую фикстуру HTML (сокращённая, но структурно верная копия реальной разметки списка)**

Файл `tests/DownloadListFromTushenka.Tests/Fixtures/list-page-sample.html`:

```html
<!DOCTYPE html>
<html>
<body>
<div class="grid grid-cols-1 items-stretch gap-4 lg:grid-cols-2">
    <div wire:key="list-group-1">
        <div class="p-3 sm:p-4">
            <div class="flex items-start gap-3">
                <a href="https://sp-mod.com/mod/2905/ragdoll-kinetics" wire:navigate class="shrink-0" aria-hidden="true" tabindex="-1">
                    <img src="https://files.sp-mod.com/mods/thumb1.png" alt="">
                </a>
                <div class="min-w-0 flex-1">
                    <div class="flex min-w-0 items-center gap-2">
                        <a href="https://sp-mod.com/mod/2905/ragdoll-kinetics" wire:navigate class="truncate font-medium text-gray-100 hover:underline">
                            Ragdoll Kinetics
                        </a>
                        <span class="shrink-0 text-xs text-gray-400">
                            1.2.0
                        </span>
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div wire:key="list-group-2">
        <div class="p-3 sm:p-4">
            <div class="flex items-start gap-3">
                <a href="https://sp-mod.com/mod/2888/task-item-indicator" wire:navigate class="shrink-0" aria-hidden="true" tabindex="-1">
                    <img src="https://files.sp-mod.com/mods/thumb2.png" alt="">
                </a>
                <div class="min-w-0 flex-1">
                    <div class="flex min-w-0 items-center gap-2">
                        <a href="https://sp-mod.com/mod/2888/task-item-indicator" wire:navigate class="truncate font-medium text-gray-100 hover:underline">
                            Task Item Indicator
                        </a>
                        <span class="shrink-0 text-xs text-gray-400">
                            1.0.0
                        </span>
                    </div>
                </div>
            </div>
        </div>
    </div>
    <div wire:key="list-group-3">
        <div class="p-3 sm:p-4">
            <div class="flex items-start gap-3">
                <a href="https://sp-mod.com/addon/39/climbable-ladders-fika-sync" wire:navigate class="shrink-0" aria-hidden="true" tabindex="-1">
                    <img src="https://files.sp-mod.com/mods/thumb3.png" alt="">
                </a>
                <div class="min-w-0 flex-1">
                    <div class="flex min-w-0 items-center gap-2">
                        <a href="https://sp-mod.com/addon/39/climbable-ladders-fika-sync" wire:navigate class="truncate font-medium text-gray-100 hover:underline">
                            Climbable Ladders - Fika sync
                        </a>
                        <span class="shrink-0 text-xs text-gray-400">
                            1.0.1
                        </span>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>
</body>
</html>
```

- [ ] **Step 7: Написать `ModEntry.cs`**

```csharp
namespace DownloadListFromTushenka;

public enum ModKind
{
    Mod,
    Addon
}

public sealed record ModEntry(ModKind Kind, int Id, string Slug, string Name, string Version);
```

- [ ] **Step 8: Написать падающий тест для `ListHtmlParser`**

Файл `tests/DownloadListFromTushenka.Tests/ListHtmlParserTests.cs`:

```csharp
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
```

- [ ] **Step 9: Запустить тесты и убедиться, что они падают**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: ошибка компиляции — `ListHtmlParser` не существует.

- [ ] **Step 10: Реализовать `ListHtmlParser.cs`**

```csharp
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
```

- [ ] **Step 11: Запустить тесты и убедиться, что они проходят**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: `Parse_ExtractsAllEntries_WithCorrectKindIdSlugNameVersion` и `Parse_ReturnsEmptyList_WhenNoEntriesFound` — PASS.

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "feat: scaffold project and add list HTML parser"
```

---

### Task 2: `ForgeApiClient` — резолв ссылки на скачивание через официальный API

**Files:**
- Create: `src/DownloadListFromTushenka/ForgeApiClient.cs`
- Create: `tests/DownloadListFromTushenka.Tests/FakeHttpMessageHandler.cs`
- Create: `tests/DownloadListFromTushenka.Tests/ForgeApiClientTests.cs`

**Interfaces:**
- Consumes: `ModEntry` (Task 1).
- Produces: `record ResolvedDownload(ModEntry Entry, string DownloadUrl, long? ContentLength)`; `interface IForgeApiClient { Task<ResolvedDownload?> ResolveDownloadAsync(ModEntry entry, CancellationToken cancellationToken); }`; `class ForgeApiClient(HttpClient httpClient) : IForgeApiClient` — используются в Task 4 (`DownloadOrchestrator`) и Task 6 (`Program.cs`).

- [ ] **Step 1: Добавить общий тестовый хелпер `FakeHttpMessageHandler`**

Файл `tests/DownloadListFromTushenka.Tests/FakeHttpMessageHandler.cs`:

```csharp
namespace DownloadListFromTushenka.Tests;

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => Task.FromResult(_responder(request));
}
```

- [ ] **Step 2: Написать падающий тест для `ForgeApiClient`**

Файл `tests/DownloadListFromTushenka.Tests/ForgeApiClientTests.cs`:

```csharp
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
              "content_length": 45393
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
        Assert.Equal(45393, result.ContentLength);
        Assert.Equal("/api/v0/mod/2905/versions", capturedRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("1.2.0", capturedRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task ResolveDownloadAsync_UsesAddonPath_ForAddonEntries()
    {
        const string json = """{ "success": true, "data": [ { "id": 1, "version": "1.0.1", "link": "https://sp-mod.com/addon/download/39/climbable-ladders-fika-sync/1.0.1", "content_length": 1024 } ] }""";

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
}
```

- [ ] **Step 3: Запустить тесты и убедиться, что они падают из-за отсутствующих типов**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: ошибка компиляции — `ForgeApiClient`, `ResolvedDownload` не существуют.

- [ ] **Step 4: Реализовать `ForgeApiClient.cs`**

```csharp
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace DownloadListFromTushenka;

public sealed record ResolvedDownload(ModEntry Entry, string DownloadUrl, long? ContentLength);

public interface IForgeApiClient
{
    Task<ResolvedDownload?> ResolveDownloadAsync(ModEntry entry, CancellationToken cancellationToken);
}

public sealed class ForgeApiClient : IForgeApiClient
{
    private readonly HttpClient _httpClient;

    public ForgeApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ResolvedDownload?> ResolveDownloadAsync(ModEntry entry, CancellationToken cancellationToken)
    {
        var kindSegment = entry.Kind == ModKind.Addon ? "addon" : "mod";
        var url = $"/api/v0/{kindSegment}/{entry.Id}/versions?filter[version]={Uri.EscapeDataString(entry.Version)}";

        ForgeVersionsResponse? response;
        try
        {
            response = await _httpClient.GetFromJsonAsync<ForgeVersionsResponse>(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        var match = response?.Data?.FirstOrDefault(v => v.Version == entry.Version);
        if (match is null || string.IsNullOrEmpty(match.Link))
        {
            return null;
        }

        return new ResolvedDownload(entry, match.Link, match.ContentLength);
    }

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
    }
}
```

- [ ] **Step 5: Запустить тесты и убедиться, что они проходят**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: все тесты, включая три новых для `ForgeApiClient` — PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: resolve download links via The Forge API"
```

---

### Task 3: `FileDownloader` — скачивание файла с дедупликацией

**Files:**
- Create: `src/DownloadListFromTushenka/FileDownloader.cs`
- Create: `tests/DownloadListFromTushenka.Tests/FileDownloaderTests.cs`

**Interfaces:**
- Consumes: `ResolvedDownload` (Task 2).
- Produces: `enum DownloadStatus { Downloaded, Skipped, Failed }`; `record DownloadOutcome(DownloadStatus Status, string? FilePath, string? ErrorMessage)`; `interface IFileDownloader { Task<DownloadOutcome> DownloadAsync(ResolvedDownload resolved, string outputDirectory, CancellationToken cancellationToken); }`; `class FileDownloader(HttpClient httpClient) : IFileDownloader` — используются в Task 4.

- [ ] **Step 1: Написать падающие тесты для `FileDownloader`**

Файл `tests/DownloadListFromTushenka.Tests/FileDownloaderTests.cs`:

```csharp
using System.Net;
using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class FileDownloaderTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "dlft-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_SavesFile_WithExtensionFromFinalRedirectedUrl()
    {
        var bytes = "fake-zip-content"u8.ToArray();
        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
            RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                "https://github.com/Hysocs/ragdollkinetics-spt/releases/download/1.2.0/RagdollKinetics-1.2.0.zip")
        });
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "1.2.0");
        var resolved = new ResolvedDownload(
            entry, "https://sp-mod.com/mod/download/2905/ragdoll-kinetics/1.2.0", bytes.Length);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Downloaded, outcome.Status);
        var expectedPath = Path.Combine(_tempDir, "ragdoll-kinetics-1.2.0.zip");
        Assert.Equal(expectedPath, outcome.FilePath);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(expectedPath));
    }

    [Fact]
    public async Task DownloadAsync_FallsBackToZipExtension_WhenUrlHasNoExtension()
    {
        var bytes = "content"u8.ToArray();
        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://example.com/download/no-extension-here")
        });
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 1, "some-mod", "Some Mod", "2.0.0");
        var resolved = new ResolvedDownload(entry, "https://sp-mod.com/mod/download/1/some-mod/2.0.0", null);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Downloaded, outcome.Status);
        Assert.Equal(Path.Combine(_tempDir, "some-mod-2.0.0.zip"), outcome.FilePath);
    }

    [Fact]
    public async Task DownloadAsync_SkipsExistingFile_WithoutOverwritingIt()
    {
        Directory.CreateDirectory(_tempDir);
        var existingPath = Path.Combine(_tempDir, "ragdoll-kinetics-1.2.0.zip");
        await File.WriteAllTextAsync(existingPath, "already here");

        var handler = new FakeHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("new-content"u8.ToArray()),
            RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                "https://github.com/example/releases/download/1.2.0/RagdollKinetics-1.2.0.zip")
        });
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 2905, "ragdoll-kinetics", "Ragdoll Kinetics", "1.2.0");
        var resolved = new ResolvedDownload(
            entry, "https://sp-mod.com/mod/download/2905/ragdoll-kinetics/1.2.0", null);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Skipped, outcome.Status);
        Assert.Equal("already here", await File.ReadAllTextAsync(existingPath));
    }

    [Fact]
    public async Task DownloadAsync_ReturnsFailed_WhenHttpRequestFails()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var httpClient = new HttpClient(handler);
        var downloader = new FileDownloader(httpClient);
        var entry = new ModEntry(ModKind.Mod, 1, "broken-mod", "Broken Mod", "1.0.0");
        var resolved = new ResolvedDownload(entry, "https://sp-mod.com/mod/download/1/broken-mod/1.0.0", null);

        var outcome = await downloader.DownloadAsync(resolved, _tempDir, CancellationToken.None);

        Assert.Equal(DownloadStatus.Failed, outcome.Status);
        Assert.NotNull(outcome.ErrorMessage);
    }
}
```

- [ ] **Step 2: Запустить тесты и убедиться, что они падают из-за отсутствующих типов**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: ошибка компиляции — `FileDownloader`, `DownloadOutcome`, `DownloadStatus` не существуют.

- [ ] **Step 3: Реализовать `FileDownloader.cs`**

```csharp
using System.Net.Http.Headers;

namespace DownloadListFromTushenka;

public enum DownloadStatus
{
    Downloaded,
    Skipped,
    Failed
}

public sealed record DownloadOutcome(DownloadStatus Status, string? FilePath, string? ErrorMessage);

public interface IFileDownloader
{
    Task<DownloadOutcome> DownloadAsync(
        ResolvedDownload resolved, string outputDirectory, CancellationToken cancellationToken);
}

public sealed class FileDownloader : IFileDownloader
{
    private readonly HttpClient _httpClient;

    public FileDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DownloadOutcome> DownloadAsync(
        ResolvedDownload resolved, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);

        try
        {
            using var response = await _httpClient.GetAsync(
                resolved.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var extension = DetermineExtension(response.Content.Headers.ContentDisposition, response.RequestMessage?.RequestUri);
            var fileName = $"{resolved.Entry.Slug}-{resolved.Entry.Version}{extension}";
            var filePath = Path.Combine(outputDirectory, fileName);

            if (File.Exists(filePath))
            {
                return new DownloadOutcome(DownloadStatus.Skipped, filePath, null);
            }

            await using (var fileStream = File.Create(filePath))
            {
                await response.Content.CopyToAsync(fileStream, cancellationToken);
            }

            return new DownloadOutcome(DownloadStatus.Downloaded, filePath, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return new DownloadOutcome(DownloadStatus.Failed, null, ex.Message);
        }
    }

    internal static string DetermineExtension(ContentDispositionHeaderValue? contentDisposition, Uri? finalUri)
    {
        var fromHeader = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        if (!string.IsNullOrWhiteSpace(fromHeader))
        {
            var trimmed = fromHeader.Trim('"');
            var ext = Path.GetExtension(trimmed);
            if (!string.IsNullOrEmpty(ext))
            {
                return ext;
            }
        }

        if (finalUri is not null)
        {
            var ext = Path.GetExtension(finalUri.AbsolutePath);
            if (!string.IsNullOrEmpty(ext))
            {
                return ext;
            }
        }

        return ".zip";
    }
}
```

- [ ] **Step 4: Запустить тесты и убедиться, что они проходят**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: все тесты, включая четыре новых для `FileDownloader` — PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: download resolved files to disk with dedup and extension detection"
```

---

### Task 4: `DownloadOrchestrator` — параллелизм, прогресс, сводка

**Files:**
- Create: `src/DownloadListFromTushenka/DownloadOrchestrator.cs`
- Create: `tests/DownloadListFromTushenka.Tests/DownloadOrchestratorTests.cs`

**Interfaces:**
- Consumes: `ModEntry` (Task 1), `IForgeApiClient`, `ResolvedDownload` (Task 2), `IFileDownloader`, `DownloadOutcome`, `DownloadStatus` (Task 3).
- Produces: `record ItemResult(ModEntry Entry, DownloadStatus Status, string? ErrorMessage)`; `record RunSummary(int Total, int Downloaded, int Skipped, int Failed, IReadOnlyList<ItemResult> FailedItems)`; `class DownloadOrchestrator(IForgeApiClient apiClient, IFileDownloader fileDownloader, TextWriter output, int maxParallelism = 4) { Task<RunSummary> RunAsync(IReadOnlyList<ModEntry> entries, string outputDirectory, CancellationToken cancellationToken) }` — используется в Task 6 (`Program.cs`).

- [ ] **Step 1: Написать падающий тест для `DownloadOrchestrator`**

Файл `tests/DownloadListFromTushenka.Tests/DownloadOrchestratorTests.cs`:

```csharp
using DownloadListFromTushenka;
using Xunit;

namespace DownloadListFromTushenka.Tests;

public class DownloadOrchestratorTests
{
    [Fact]
    public async Task RunAsync_ProducesSummary_WithMixedOutcomes_AndProcessesEveryEntryExactlyOnce()
    {
        var entries = new[]
        {
            new ModEntry(ModKind.Mod, 1, "mod-a", "Mod A", "1.0.0"),
            new ModEntry(ModKind.Mod, 2, "mod-b", "Mod B", "2.0.0"),
            new ModEntry(ModKind.Mod, 3, "mod-c", "Mod C", "3.0.0"),
        };

        var apiClient = new StubForgeApiClient(entry => entry.Id == 3
            ? null
            : new ResolvedDownload(entry, $"https://example.com/{entry.Slug}.zip", 100));

        var downloader = new StubFileDownloader(entry => entry.Id == 2
            ? new DownloadOutcome(DownloadStatus.Skipped, "/tmp/mod-b-2.0.0.zip", null)
            : new DownloadOutcome(DownloadStatus.Downloaded, $"/tmp/{entry.Slug}.zip", null));

        var writer = new StringWriter();
        var orchestrator = new DownloadOrchestrator(apiClient, downloader, writer, maxParallelism: 2);

        var summary = await orchestrator.RunAsync(entries, "/tmp/out", CancellationToken.None);

        Assert.Equal(3, summary.Total);
        Assert.Equal(1, summary.Downloaded);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(1, summary.Failed);
        Assert.Single(summary.FailedItems);
        Assert.Equal("Mod C", summary.FailedItems[0].Entry.Name);
        Assert.Contains("версия не найдена", summary.FailedItems[0].ErrorMessage);
        Assert.Equal(3, downloader.CallCount + apiClient.FailedCallCount(entries));
    }
}

internal sealed class StubForgeApiClient : IForgeApiClient
{
    private readonly Func<ModEntry, ResolvedDownload?> _resolve;

    public StubForgeApiClient(Func<ModEntry, ResolvedDownload?> resolve) => _resolve = resolve;

    public Task<ResolvedDownload?> ResolveDownloadAsync(ModEntry entry, CancellationToken cancellationToken)
        => Task.FromResult(_resolve(entry));

    public int FailedCallCount(IEnumerable<ModEntry> entries) => entries.Count(e => _resolve(e) is null);
}

internal sealed class StubFileDownloader : IFileDownloader
{
    private readonly Func<ModEntry, DownloadOutcome> _download;
    public int CallCount { get; private set; }

    public StubFileDownloader(Func<ModEntry, DownloadOutcome> download) => _download = download;

    public Task<DownloadOutcome> DownloadAsync(
        ResolvedDownload resolved, string outputDirectory, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_download(resolved.Entry));
    }
}
```

- [ ] **Step 2: Запустить тесты и убедиться, что они падают из-за отсутствующих типов**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: ошибка компиляции — `DownloadOrchestrator`, `RunSummary`, `ItemResult` не существуют.

- [ ] **Step 3: Реализовать `DownloadOrchestrator.cs`**

```csharp
namespace DownloadListFromTushenka;

public sealed record ItemResult(ModEntry Entry, DownloadStatus Status, string? ErrorMessage);

public sealed record RunSummary(
    int Total,
    int Downloaded,
    int Skipped,
    int Failed,
    IReadOnlyList<ItemResult> FailedItems);

public sealed class DownloadOrchestrator
{
    private readonly IForgeApiClient _apiClient;
    private readonly IFileDownloader _fileDownloader;
    private readonly TextWriter _output;
    private readonly int _maxParallelism;

    public DownloadOrchestrator(
        IForgeApiClient apiClient,
        IFileDownloader fileDownloader,
        TextWriter output,
        int maxParallelism = 4)
    {
        _apiClient = apiClient;
        _fileDownloader = fileDownloader;
        _output = output;
        _maxParallelism = maxParallelism;
    }

    public async Task<RunSummary> RunAsync(
        IReadOnlyList<ModEntry> entries, string outputDirectory, CancellationToken cancellationToken)
    {
        var results = new ItemResult[entries.Count];
        var nextIndex = 0;
        var completed = 0;
        var progressLock = new object();

        async Task WorkerAsync()
        {
            while (true)
            {
                int index;
                lock (progressLock)
                {
                    if (nextIndex >= entries.Count)
                    {
                        return;
                    }
                    index = nextIndex++;
                }

                var result = await ProcessEntryAsync(entries[index], outputDirectory, cancellationToken);
                results[index] = result;

                lock (progressLock)
                {
                    completed++;
                    _output.WriteLine(FormatProgressLine(completed, entries.Count, result));
                }
            }
        }

        var workerCount = Math.Min(_maxParallelism, Math.Max(1, entries.Count));
        var workers = Enumerable.Range(0, workerCount).Select(_ => WorkerAsync());
        await Task.WhenAll(workers);

        var downloaded = results.Count(r => r.Status == DownloadStatus.Downloaded);
        var skipped = results.Count(r => r.Status == DownloadStatus.Skipped);
        var failed = results.Where(r => r.Status == DownloadStatus.Failed).ToList();

        return new RunSummary(entries.Count, downloaded, skipped, failed.Count, failed);
    }

    private async Task<ItemResult> ProcessEntryAsync(
        ModEntry entry, string outputDirectory, CancellationToken cancellationToken)
    {
        var resolved = await _apiClient.ResolveDownloadAsync(entry, cancellationToken);
        if (resolved is null)
        {
            return new ItemResult(entry, DownloadStatus.Failed, "версия не найдена в API");
        }

        var outcome = await _fileDownloader.DownloadAsync(resolved, outputDirectory, cancellationToken);
        return new ItemResult(entry, outcome.Status, outcome.ErrorMessage);
    }

    private static string FormatProgressLine(int completed, int total, ItemResult result)
    {
        var status = result.Status switch
        {
            DownloadStatus.Downloaded => "OK",
            DownloadStatus.Skipped => "SKIP (уже скачан)",
            DownloadStatus.Failed => $"ОШИБКА: {result.ErrorMessage}",
            _ => result.Status.ToString()
        };
        return $"[{completed}/{total}] {result.Entry.Name} {result.Entry.Version} ... {status}";
    }
}
```

- [ ] **Step 4: Запустить тесты и убедиться, что они проходят**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: все тесты, включая новый для `DownloadOrchestrator` — PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: orchestrate resolve+download with bounded parallelism and summary"
```

---

### Task 5: `ArgumentParser` — разбор аргументов командной строки

**Files:**
- Create: `src/DownloadListFromTushenka/ArgumentParser.cs`
- Create: `tests/DownloadListFromTushenka.Tests/ArgumentParserTests.cs`

**Interfaces:**
- Produces: `record ParsedArgs(string ListUrl, string OutputDirectory)`; `static class ArgumentParser { static ParsedArgs? Parse(string[] args) }` — используется в Task 6 (`Program.cs`).

- [ ] **Step 1: Написать падающий тест для `ArgumentParser`**

Файл `tests/DownloadListFromTushenka.Tests/ArgumentParserTests.cs`:

```csharp
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
```

- [ ] **Step 2: Запустить тесты и убедиться, что они падают из-за отсутствующих типов**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: ошибка компиляции — `ArgumentParser`, `ParsedArgs` не существуют.

- [ ] **Step 3: Реализовать `ArgumentParser.cs`**

```csharp
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
```

- [ ] **Step 4: Запустить тесты и убедиться, что они проходят**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: все тесты, включая пять новых для `ArgumentParser` — PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: parse CLI arguments"
```

---

### Task 6: `Program.cs` — обвязка CLI

**Files:**
- Modify: `src/DownloadListFromTushenka/Program.cs` (полностью заменить сгенерированный шаблон)

**Interfaces:**
- Consumes: всё из Task 1–5 (`ArgumentParser`, `ListHtmlParser`, `ForgeApiClient`, `FileDownloader`, `DownloadOrchestrator`).
- Produces: точка входа процесса, код возврата (`0` — успех, `1` — фатальная ошибка, `2` — завершилось с ошибками по отдельным модам).

- [ ] **Step 1: Заменить содержимое `Program.cs`**

```csharp
using DownloadListFromTushenka;

var parsedArgs = ArgumentParser.Parse(args);
if (parsedArgs is null)
{
    Console.Error.WriteLine("Использование: DownloadListFromTushenka.exe <ссылка-на-список> [--out <папка>]");
    return 1;
}

const string userAgent = "DownloadListFromTushenka/1.0 (mod list downloader)";

using var pageAndApiHttpClient = new HttpClient
{
    BaseAddress = new Uri("https://sp-mod.com"),
    Timeout = TimeSpan.FromSeconds(30)
};
pageAndApiHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

using var downloadHttpClient = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(5)
};
downloadHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

Console.WriteLine($"Загружаю список: {parsedArgs.ListUrl}");

string html;
try
{
    html = await pageAndApiHttpClient.GetStringAsync(parsedArgs.ListUrl);
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Не удалось загрузить страницу списка: {ex.Message}");
    return 1;
}

var entries = ListHtmlParser.Parse(html);
if (entries.Count == 0)
{
    Console.Error.WriteLine("На странице не найдено ни одного мода/аддона. Проверьте ссылку.");
    return 1;
}

Console.WriteLine($"Найдено записей: {entries.Count}");
Console.WriteLine($"Папка вывода: {parsedArgs.OutputDirectory}");
Console.WriteLine();

var apiClient = new ForgeApiClient(pageAndApiHttpClient);
var fileDownloader = new FileDownloader(downloadHttpClient);
var orchestrator = new DownloadOrchestrator(apiClient, fileDownloader, Console.Out);

var summary = await orchestrator.RunAsync(entries, parsedArgs.OutputDirectory, CancellationToken.None);

Console.WriteLine();
Console.WriteLine(
    $"Готово: скачано {summary.Downloaded}, пропущено {summary.Skipped}, ошибок {summary.Failed} из {summary.Total}.");

if (summary.FailedItems.Count > 0)
{
    Console.WriteLine("С ошибками:");
    foreach (var item in summary.FailedItems)
    {
        Console.WriteLine($"  - {item.Entry.Name} {item.Entry.Version}: {item.ErrorMessage}");
    }
}

return summary.Failed > 0 ? 2 : 0;
```

- [ ] **Step 2: Собрать проект**

```bash
dotnet build src/DownloadListFromTushenka
```

Ожидается: сборка без ошибок.

- [ ] **Step 3: Прогнать полный набор тестов (регрессия)**

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Ожидается: все тесты из Task 1–5 — PASS.

- [ ] **Step 4: Проверить CLI без аргументов**

```bash
dotnet run --project src/DownloadListFromTushenka
```

Ожидается: печатается строка использования в stderr, код возврата `1` (`echo $?` после запуска на Windows — `echo %ERRORLEVEL%` в cmd или `$LASTEXITCODE` в PowerShell).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: wire CLI entry point"
```

---

### Task 7: Публикация self-contained exe и сквозная проверка на реальном списке

**Files:**
- Не создаёт новых исходников — только публикация и ручная проверка.

- [ ] **Step 1: Опубликовать self-contained single-file exe**

```bash
dotnet publish src/DownloadListFromTushenka -c Release
```

Ожидается: в `src/DownloadListFromTushenka/bin/Release/net8.0/win-x64/publish/` появляется `DownloadListFromTushenka.exe` (один файл, без соседних `.dll` зависимостей рантайма).

- [ ] **Step 2: Запустить exe на тестовой ссылке пользователя**

```bash
cd src/DownloadListFromTushenka/bin/Release/net8.0/win-x64/publish
./DownloadListFromTushenka.exe "https://sp-mod.com/list/124032/sm-41x/mJ5siXVM37ia89SoiKNbgnr4oAUxmuqN"
```

Ожидается: в консоли построчный прогресс по каждой записи (`[N/63] Имя Версия ... OK`), в конце — сводка вида `Готово: скачано 63, пропущено 0, ошибок 0 из 63.` (число может отличаться, если список на сайте изменился с момента написания плана — ориентир: «62 mods · 1 addon» на странице списка).

- [ ] **Step 3: Проверить содержимое папки `Download`**

```bash
ls -la Download
```

Ожидается: количество файлов соответствует числу успешно скачанных записей из сводки; имена вида `{slug}-{version}.zip`.

- [ ] **Step 4: Проверить идемпотентность повторного запуска**

```bash
./DownloadListFromTushenka.exe "https://sp-mod.com/list/124032/sm-41x/mJ5siXVM37ia89SoiKNbgnr4oAUxmuqN"
```

Ожидается: все ранее скачанные записи помечены `SKIP (уже скачан)`, ни один файл не перезаписан (проверить по времени изменения файлов — не изменилось).

- [ ] **Step 5: Зафиксировать результат в README**

Создать `README.md` в корне проекта:

```markdown
# DownloadListFromTushenka

Консольная утилита для Windows: скачивает все моды и аддоны из
публичного/по-ссылке списка на sp-mod.com («The Forge») в папку
`Download`, используя официальный открытый API сайта.

## Использование

```
DownloadListFromTushenka.exe <ссылка-на-список> [--out <папка>]
```

- `<ссылка-на-список>` — ссылка вида `https://sp-mod.com/list/{id}/{slug}/{token}`.
- `--out` — необязательный путь к папке вывода (по умолчанию `Download`
  рядом с exe).

Уже скачанные файлы при повторном запуске не перекачиваются.

## Сборка

Требуется .NET 8 SDK.

```bash
dotnet publish src/DownloadListFromTushenka -c Release
```

Готовый `DownloadListFromTushenka.exe` — в
`src/DownloadListFromTushenka/bin/Release/net8.0/win-x64/publish/`.
Запускается на чистой Windows без установки .NET.

## Тесты

```bash
dotnet test tests/DownloadListFromTushenka.Tests
```

Спецификация: [docs/specs/2026-09-03-mod-list-downloader-design.md](docs/specs/2026-09-03-mod-list-downloader-design.md)
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "docs: add README with usage and build instructions"
```
