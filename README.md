# Sprout Studio

Sprout Studio is the Windows home for the Sprout programming language. The repository contains a Rust language engine, project system, CLI, versioned C ABI, and a native C#/.NET 10/Avalonia 12 desktop application. Together they create, validate, load, check, debug, build, and execute Guided, Structured, and Advanced Sprout projects.

Phase 9 turns Studio into a cohesive multi-mode IDE: engine-backed editing and recorded debugging; integrated Academy, rendered offline Documentation, project Search, Source Control, Packages, and Toolchains; validated interpreted bundle builds; multi-tab workspaces; and honest edit/run/build adapters for additional languages. See [docs/architecture.md](docs/architecture.md) and [docs/advanced-ide.md](docs/advanced-ide.md) for the current boundaries.

## Try Sprout

Install a current Rust toolchain, clone the repository, and run:

```powershell
cargo run -p sprout-cli -- run examples/hello.spr
```

Expected output:

```text
Hello Sprout
Hello Sprout
Hello Sprout
```

Validate without executing:

```powershell
cargo run -p sprout-cli -- check examples/hello.spr
```

Run the other syntax profiles:

```powershell
cargo run -p sprout-cli -- run --profile structured examples/structured.spr
cargo run -p sprout-cli -- run --profile advanced examples/advanced.spr
```

Prepare the pinned development runtime and run the Python example:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/prepare-python-runtime.ps1
cargo run -p sprout-cli -- run examples/python.spr
```

Create and run a complete project:

```powershell
cargo run -p sprout-cli -- new HelloSprout --template guided
cargo run -p sprout-cli -- run HelloSprout
```

Python packages declared under `[python]` in `sprout.toml` are installed only into that project's `.sprout/python` directory:

```powershell
cargo run -p sprout-cli -- python sync HelloSprout --python runtime/python-3.13.14-win-x64/python.exe
```

## Develop

```powershell
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
dotnet run --project studio/tests/SproutStudio.Tests/SproutStudio.Tests.csproj
dotnet build studio/SproutStudio.sln -c Debug -p:Platform=x64
```

Detailed build instructions are in [docs/building.md](docs/building.md), advanced editor/debugger behavior is documented in [docs/advanced-ide.md](docs/advanced-ide.md), Academy behavior is documented in [docs/academy.md](docs/academy.md), toolchain behavior is documented in [docs/toolchains.md](docs/toolchains.md), Python interoperability is documented in [docs/python-interop.md](docs/python-interop.md), the project format is documented in [docs/projects.md](docs/projects.md), and the desktop workflow is described in [docs/studio.md](docs/studio.md). The implemented language is documented in [docs/grammar.md](docs/grammar.md) and [docs/syntax-profiles.md](docs/syntax-profiles.md).

Create the validated self-contained Windows release, installer, portable archive, and SHA-256 checksums with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build-release.ps1
```

Release and installer details are in [docs/releasing.md](docs/releasing.md).

## License

MIT
