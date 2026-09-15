# Download List From Tushenka (DLFT)

A small Windows tool that downloads SPT mods from a [sp-mod.com](https://sp-mod.com)
("The Forge") mod list and installs the archives straight into an SPT game
folder — no manual unzipping, no hunting for the right subfolder.

## Get it

Grab `DownloadListFromTushenka.exe` from a [release](../../releases) and run
it — single file, self-contained, nothing else needed alongside it.

Three tabs: **Instructions**, **Download mods**, **Install mods**.

## What it does

- Downloads every mod/addon in a list, skipping ones already on disk.
- Cross-checks each mod's version against SPT 4.1.x compatibility and
  falls back to the newest confirmed release if the listed one is stale.
- Installs `.zip`, `.7z` and `.rar` archives, sniffing the real format
  instead of trusting the file extension.
- Handles Cyrillic filenames inside `.zip` correctly.
- Optional: skip readme/license/changelog files on install.
- Light/dark theme and RU/EN language, both switchable instantly.

## Project layout

- `src/DownloadListFromTushenka.App` — the WPF GUI (`DownloadListFromTushenka.exe`).
- `src/DownloadListFromTushenka.Downloader` — console downloader, same core logic.
- `src/DownloadListFromTushenka.Installer` — console installer, same core logic.
- `tests/DownloadListFromTushenka.Tests` — xUnit tests for both.

The GUI project references the two console projects and reuses their
classes directly — there's one copy of the actual download/install logic,
not three.

## Build

Requires the .NET 10 SDK.

```bash
dotnet publish src/DownloadListFromTushenka.App -c Release -r win-x64
```

Output: `src/DownloadListFromTushenka.App/bin/Release/net10.0-windows/win-x64/publish/DownloadListFromTushenka.exe`.

## Test

```bash
dotnet test
```

## License

MIT — see [LICENSE](LICENSE).
