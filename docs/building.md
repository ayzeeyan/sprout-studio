# Building Sprout Studio

## Development requirements

- Windows x86-64
- .NET 10 SDK
- current stable Rust toolchain with `x86_64-pc-windows-msvc`, Clippy, and rustfmt
- MSVC linker/build tools and a Windows SDK
- Git
- NSIS 3 only when producing the Windows Setup executable

Sprout Studio uses open-source Avalonia 12 packages from NuGet. It does not use Windows App SDK, WinUI, a WebView, or paid Avalonia components. The pinned x64 CPython runtime is prepared automatically, so supported Python interoperability does not use a system Python installation.

## Development build

From the repository root:

```powershell
dotnet restore studio/SproutStudio.sln --configfile studio/NuGet.Config -p:Platform=x64
dotnet build studio/SproutStudio.sln -c Debug -p:Platform=x64 --no-restore
dotnet run --project studio/tests/SproutStudio.Tests/SproutStudio.Tests.csproj -c Debug -p:Platform=x64 --no-build
```

The desktop build invokes Cargo for the matching configuration and copies the repository-built `sprout_ffi.dll`, `sprout.exe`, and pinned Python runtime beside `SproutStudio.exe`. It never searches `PATH` for an engine DLL.

Run the application from:

```text
studio/src/SproutStudio.Desktop/bin/x64/Debug/net10.0/win-x64/SproutStudio.exe
```

## Rust development

Prepare the pinned runtime once before direct Cargo commands:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/prepare-python-runtime.ps1
cargo build --workspace
cargo run -p sprout-cli -- run examples/hello.spr
```

The Python preparation script downloads the official CPython 3.13.14 embeddable distribution and pip 26.2.1, verifies pinned SHA-256 hashes, and stores them under ignored `runtime/` output.

## Validation

```powershell
cargo fmt --all --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo test --workspace --release --locked
dotnet build studio/SproutStudio.sln -c Release -p:Platform=x64
dotnet run --project studio/tests/SproutStudio.Tests/SproutStudio.Tests.csproj -c Release -p:Platform=x64 --no-build
```

The .NET integration harness invokes the real release/debug Rust DLL and covers language intelligence, structured diagnostics, execution, Unicode spans, formatting, debugger snapshots, Canvas commands, projects, dependencies, bundle builds, multi-document editing, atomic saves, process cancellation, Rust/Go/Python/Java detection, Academy state, and startup of the real Avalonia Academy and Toolchains windows.

For the complete publish and installer pipeline, see [releasing.md](releasing.md).
