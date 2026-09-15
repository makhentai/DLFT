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

See [ARCHITECTURE.md](ARCHITECTURE.md) for how it's built, project layout,
and how to build/test it yourself.

## License

MIT — see [LICENSE](LICENSE).
