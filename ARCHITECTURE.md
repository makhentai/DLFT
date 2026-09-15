# How DLFT works

A tour through the code for anyone curious enough to read past the README.

## Project layout

- `src/DownloadListFromTushenka.App` — the WPF GUI (`DownloadListFromTushenka.exe`).
- `src/DownloadListFromTushenka.Downloader` — console downloader, same core logic.
- `src/DownloadListFromTushenka.Installer` — console installer, same core logic.
- `tests/DownloadListFromTushenka.Tests` — xUnit tests for both.

## Build & test

Requires the .NET 10 SDK.

```bash
dotnet publish src/DownloadListFromTushenka.App -c Release -r win-x64
dotnet test
```

Output exe: `src/DownloadListFromTushenka.App/bin/Release/net10.0-windows/win-x64/publish/DownloadListFromTushenka.exe`.

## Why three projects instead of one

`DownloadListFromTushenka.App` (the GUI), `.Downloader` and `.Installer`
(the two original console tools) are separate projects, but the GUI
doesn't reimplement anything — it has a `ProjectReference` to both console
projects and calls their classes directly (`ForgeApiClient`,
`DownloadOrchestrator`, `ModArchiveInstaller`, the archive readers, all of
it). The console tools still build and work standalone; the GUI is a
third consumer of the same core code, not a rewrite of it.

One consequence: the GUI's live log is English, but that's a
**GUI-only translation layer** (see `GuiLogWriter`/`LogTranslator` below).
The core classes still emit Russian strings, because the console tools
were written for a Russian-speaking audience first and still ship that
way.

## Downloading

**`ListHtmlParser`** scrapes the sp-mod.com list page with HtmlAgilityPack:
it walks every `<a href>` matching `/mod/{id}/{slug}` or `/addon/{id}/{slug}`,
then looks for a sibling `<span>` with the site's version-number CSS classes
to read the mod's currently-listed version string. That's it — no API call
yet, just turning one HTML page into a list of `(kind, id, slug, name,
version)` tuples.

**`ForgeApiClient`** turns each of those into an actual download URL. This
is the part with a real bug fix behind it: the Forge API's
`/versions` endpoint returns every published version of a mod, each
tagged with an `spt_version_constraint` string (`"~4.1.5"`, `">=4.0.11
<4.1.0"`, or a bare version). The client:

1. Fetches the version matching what the list page showed.
2. Checks whether *that* version's constraint actually covers the
   4.1.0–4.2.0 range this project targets.
3. If not — e.g. the list page was scraped before the mod's newest
   4.1.x-compatible release existed — it fetches the mod's *entire*
   version history and picks the newest one that does satisfy the
   range, instead of trusting a possibly-stale scrape.
4. If nothing is explicitly confirmed for 4.1.x, it falls back to the
   newest version available at all, but tags the result with a warning
   string that ends up in the log.

The constraint parser (`ForgeApiClient.ParseConstraint`) handles `~`
(tilde — same major.minor), `^` (caret — same major), explicit
`>=X <Y` ranges, and bare version numbers (treated as an exact pin, not
a minimum) — that's the full set of formats actually seen on Forge.

**`FileDownloader`** does the actual HTTP download, computing the file
extension from the `Content-Disposition` header or the final redirected
URL (Forge doesn't always name files predictably), and skips a mod
entirely if a matching `{slug}-{version}.*` file already exists —
without any network call, so re-running a download after an interruption
is fast.

**`DownloadOrchestrator`** runs up to 4 downloads in parallel with a
simple pull-based worker pool (each worker grabs the next index from a
shared counter under a lock) and writes one progress line per completed
item to whatever `TextWriter` it's given — `Console.Out` for the console
tool, a custom writer for the GUI.

## Installing

**`ArchiveFormatSniffer`** reads the first few bytes of a file and checks
them against the real ZIP/7z/RAR magic numbers, because some mods on
Forge are uploaded with the wrong file extension. `CompositeArchiveReader`
uses that sniff result first and only falls back to the file extension if
the content is unrecognized — RAR archives are routed to the same 7z-based
reader, since the bundled 7z.dll can read RAR too.

**`ZipArchiveReader`** wraps `System.IO.Compression.ZipFile`, but with one
deliberate fix: entries without the ZIP "UTF-8 names" flag set are decoded
as Windows-1251 instead of .NET's default IBM437. Plenty of older mod
archives store Cyrillic filenames that way, and IBM437 turns them into
garbage. (There's a hand-built raw ZIP byte fixture in the test suite that
reproduces this exact failure mode — see `ZipArchiveReaderTests`.)

**`SevenZipDllArchiveReader`** wraps the `SevenZipExtractor` NuGet package,
which itself wraps 7-Zip's real `7z.dll` for fast large-archive extraction.
The interesting part: `7z.dll` is a native library loaded via a real
Win32 `LoadLibrary` call on a file path — it can't be loaded straight out
of a single-file .NET bundle. So the DLL is embedded as a plain
`EmbeddedResource` in the assembly, and on first use `ExtractLibraryToTempFile`
copies it out to `%TEMP%\DownloadListFromTushenka-native\7z.dll` once (skipped
on later runs if the file's already there with the right size) and hands
that real path to the extractor. End result: one `.exe`, no companion
`x64\` folder, and the native library still loads correctly.

**`ArchiveEntryPathResolver`** is the shared path-safety logic both
readers call before writing any file: it resolves the entry's path
against the destination root and refuses to write anywhere outside it
(zip-slip protection), and returns `null` (skip) if the target exists and
overwrite wasn't confirmed.

**`ModArchiveInstaller`** ties it together: lists every archive's entries
first (to count conflicts up front), asks the overwrite question *once*
for however many files actually conflict — not once per file — retries a
failed extraction up to 3 times (antivirus tools sometimes hold a
just-written `.dll` for a moment), and optionally skips anything
`DocFileFilter` recognizes as a readme/license/changelog by filename.

## The GUI layer

Everything under `src/DownloadListFromTushenka.App` is presentation —
`MainWindow.xaml`/`.xaml.cs` drive the same core classes above through a
few small adapters:

- **`Loc`** is a hand-rolled runtime localizer: a `Dictionary<string,
  (string Ru, string En)>` exposed through an indexer, bound from XAML as
  `{Binding Source={x:Static app:Loc.Instance}, Path=[SomeKey]}`. Calling
  `Loc.Toggle()` raises `PropertyChanged` with a `null` property name,
  which WPF's binding engine treats as "re-evaluate everything bound to
  this object" — so every bound string in the window updates in place,
  no ResX/satellite-assembly machinery needed.
- **`ThemeManager`** swaps the second entry in
  `Application.Current.Resources.MergedDictionaries` between
  `Themes/Light.xaml` and `Themes/Dark.xaml` — two flat brush
  dictionaries with identical keys. Every style in `Styles.xaml` (the
  first, unchanging merged dictionary) references those keys with
  `DynamicResource`, not `StaticResource`, which is what makes the swap
  visible immediately instead of only on next window creation.
- **`GuiLogWriter`** is a `TextWriter` that the core orchestrators write
  their progress lines to. It runs each line through `LogTranslator`
  (a fixed set of regexes matching the small number of Russian message
  shapes the core code actually produces) before appending it to a
  `RichTextBox` as a new colored `Paragraph` — red for `ERROR`, amber for
  `WARNING`, green for `OK`/`done:`. Colors are looked up via
  `SetResourceReference` rather than a resolved `Brush`, so they follow
  theme changes too.
- **`BusySpinner`** exists purely because a `ProgressBar` that only moves
  once per completed download doesn't feel alive during one large,
  slow file — it's a `DispatcherTimer` cycling a small braille spinner
  character appended to whatever status text is current.

## A couple of bugs worth knowing about (in case you hit them again)

- **`InvariantGlobalization` and WPF don't mix.** It's fine — even
  recommended — for the two console tools, but WPF's font/text-layout
  engine (`MS.Internal.FontCache`) needs real culture data the first
  time it lays out a script it hasn't seen yet, and throws
  `CultureNotFoundException` if that's unavailable. This can surface
  well after startup, on whatever UI element first triggers it. The
  App project explicitly does *not* set this flag; the console projects
  still do.
- **`Run.Text` bindings default to `TwoWay`.** Binding a `<Run>` inside a
  `<TextBlock>` to a read-only indexer property throws at load time
  unless you add `Mode=OneWay` explicitly — WPF tries to write back into
  the indexer because that's `Run.Text`'s default binding mode, and the
  indexer has no setter.
