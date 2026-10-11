# Windows releases

`scripts/build-release.ps1` is the authoritative release entry point. It validates the single `VERSION` file against the Cargo workspace, prepares the pinned Python runtime, restores NuGet, builds the Rust workspace in Release, runs Rust and .NET validation, publishes the Avalonia application self-contained for `win-x64`, builds the NSIS installer, creates an optional portable archive, and writes SHA-256 checksums.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build-release.ps1
```

The default release restores from `nuget.org` and performs NuGet's package audit. In a deliberately network-isolated build environment with a complete existing package cache, pass `-OfflineRestore`; unreachable sources are ignored, NuGet's online audit is disabled for that run, and any package missing from the cache remains a hard failure. Production CI uses the default online path.

Generated output is ignored by Git:

```text
artifacts/release/SproutStudio-Setup-x64.exe
artifacts/release/SproutStudio-Setup.exe          (identical copy used by in-app update checks)
artifacts/release/SproutStudio-Portable-x64.zip
artifacts/release/SHA256SUMS.txt
```

The script finds NSIS 3 in Program Files, `%LOCALAPPDATA%\Programs\NSIS`, a Scoop installation (`scoop bucket add extras; scoop install nsis`), or on `PATH`.

## Publishing on GitHub

Pushing a `v*` tag that matches `VERSION` (for example `v0.2.0`) runs `.github/workflows/release.yml`, which rebuilds and validates everything on a clean Windows runner and attaches the installer, portable archive, and checksums to a GitHub Release. To publish a locally built release instead, create the release by hand and upload all four files from `artifacts/release`; Studio's update check looks for `SproutStudio-Setup.exe` and `SHA256SUMS.txt` on the latest release.

## Installer decision

Sprout Studio uses NSIS 3 for the initial Setup EXE. NSIS is mature, scriptable, supports per-user installation and silent operation, and its core/source/plugins/documentation use the permissive zlib/libpng license. The decision was checked against the official [NSIS license appendix](https://nsis.sourceforge.io/Docs/AppendixI.html) and [official download/release page](https://nsis.sourceforge.io/Download).

The installer uses a stable per-user location under `%LOCALAPPDATA%\Programs\Sprout Studio`, registers normal Windows uninstall metadata in the 64-bit registry view, creates Start Menu shortcuts, offers an optional desktop shortcut, and optionally associates only `.spr` with the friendly `Sprout Source File` class. Setup reads back its product, uninstall, and association registration before reporting success. Installing a newer version upgrades the same stable installation and removes legacy 32-bit registration. Uninstall removes the exact program directory, shortcuts, association, and uninstall metadata from both registry views; it does not delete projects or mutable data under `%LOCALAPPDATA%\SproutStudio`.

## Signing

Unsigned development releases make no publisher-trust claim. For an authorized production certificate, set `SPROUT_SIGN_CERT_SHA1` before running the script. `SPROUT_SIGNTOOL_PATH` may identify a non-standard Windows SDK `signtool.exe`. The pipeline signs the application, CLI, native engine, and final Setup executable with SHA-256 and an RFC 3161 timestamp.

## Reproducibility and CI

Installer input comes only from `artifacts/publish/win-x64`; it never reaches into a Debug directory. `.github/workflows/release.yml` runs the same script on Windows for manual dispatches and version tags. The resulting installer is the primary distribution; the portable archive is secondary.
