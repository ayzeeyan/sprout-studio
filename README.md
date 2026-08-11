# Sprout Studio

Sprout Studio is the Windows home for the Sprout programming language. The repository contains a dependency-free Rust engine and CLI plus the Phase 4 C#/WinUI 3 desktop application. Together they tokenize, parse, analyze, and execute Guided, Structured, and Advanced Sprout source.

The project system, Python interoperability, toolchain manager, and Academy are deliberately deferred. See [docs/architecture.md](docs/architecture.md) for the current boundaries and roadmap status.

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

## Develop

```powershell
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
dotnet run --project studio/tests/SproutStudio.Tests/SproutStudio.Tests.csproj
dotnet build studio/src/SproutStudio/SproutStudio.csproj -c Debug -p:Platform=x64
```

Detailed build instructions are in [docs/building.md](docs/building.md), and the desktop workflow is described in [docs/studio.md](docs/studio.md). The implemented language is documented in [docs/grammar.md](docs/grammar.md) and [docs/syntax-profiles.md](docs/syntax-profiles.md).

## License

MIT
