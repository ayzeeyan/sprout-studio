# Building Sprout Phase 6

## Development requirements

- Windows 10 version 1809 or newer for Sprout Studio
- Rust 1.95 or a compatible newer stable toolchain
- .NET 8 SDK
- Visual Studio with the WinUI application development workload (for IDE development)

WinUI 3 is restored through the pinned `Microsoft.WindowsAppSDK` NuGet package; a separate Windows App SDK download is not required. Python interoperability uses a bundled x64 CPython runtime, so a separate system Python installation is not required after preparing the development runtime.

Prepare the pinned runtime once before direct Cargo development:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/prepare-python-runtime.ps1
```

The script downloads the official CPython 3.13.14 embeddable distribution and pip 26.2.1, verifies their pinned SHA-256 hashes, and stores them under the ignored `runtime/` directory. Cargo copies that runtime beside Python-enabled development binaries and tests. Studio builds invoke the preparation script automatically when necessary.

## Build

From the repository root:

```powershell
cargo build --workspace
```

The development CLI executable is produced under `target/debug` (`sprout.exe` on Windows).

For an optimized binary:

```powershell
cargo build --release -p sprout-cli
```

Build the Windows Studio application for x64:

```powershell
dotnet restore studio/SproutStudio.sln --configfile studio/NuGet.Config -p:Platform=x64
dotnet build studio/src/SproutStudio/SproutStudio.csproj -c Release -p:Platform=x64 --no-restore
```

The Studio build compiles `sprout-ffi` and copies both `sprout_ffi.dll` and the pinned Python runtime beside the application automatically.

Create a self-contained CLI directory containing `sprout.exe` and the same Python runtime:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/package-cli.ps1 -Configuration Release
```

The result is written to `artifacts/sprout-cli-win-x64`.

## Validate

```powershell
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo run -p sprout-cli -- run examples/hello.spr
dotnet run --project studio/tests/SproutStudio.Tests/SproutStudio.Tests.csproj -p:Platform=x64
dotnet build studio/src/SproutStudio/SproutStudio.csproj -c Debug -p:Platform=x64 --no-restore
```

The .NET integration harness creates real projects through the Rust DLL, checks invalid manifests, executes embedded Python with project-local package isolation, verifies structured diagnostic metadata and Unicode editor navigation, enforces recent-project persistence limits, tests atomic Save and Save As behavior, executes an edited project, and launches the real WinUI executable. A test fails if Studio exits before creating a responsive main window or cannot close normally.

The Rust toolchain, .NET SDK, and Visual Studio are development requirements only. Studio build output contains the Sprout engine and embedded Python runtime; packaging it into an installer remains deferred.
