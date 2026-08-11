# Building Sprout Phase 4

## Development requirements

- Windows 10 version 1809 or newer for Sprout Studio
- Rust 1.95 or a compatible newer stable toolchain
- .NET 8 SDK
- Visual Studio with the WinUI application development workload (for IDE development)

The engine and CLI have no runtime dependencies. WinUI 3 is restored through the pinned `Microsoft.WindowsAppSDK` NuGet package; a separate Windows App SDK download is not required.

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

The Studio build compiles `sprout-ffi` and copies `sprout_ffi.dll` beside the application automatically.

## Validate

```powershell
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo run -p sprout-cli -- run examples/hello.spr
dotnet run --project studio/tests/SproutStudio.Tests/SproutStudio.Tests.csproj
dotnet build studio/src/SproutStudio/SproutStudio.csproj -c Debug -p:Platform=x64 --no-restore
```

The Rust toolchain is a development requirement only. A future Sprout Studio installer will bundle the engine and runtime so end users do not need Rust, Cargo, Visual Studio, or the .NET SDK.
