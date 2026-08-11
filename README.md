# Sprout Studio

Sprout Studio is the future Windows home for the Sprout programming language. This repository currently contains the completed Phase 3 language foundation: a dependency-free Rust engine and CLI that tokenize, parse, analyze, and execute Guided, Structured, and Advanced Sprout source.

The Studio GUI, project system, Python interoperability, toolchain manager, and Academy are deliberately deferred. See [docs/architecture.md](docs/architecture.md) for the current boundaries and roadmap status.

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
```

Detailed build instructions are in [docs/building.md](docs/building.md). The implemented language is documented in [docs/grammar.md](docs/grammar.md) and [docs/syntax-profiles.md](docs/syntax-profiles.md).

## License

MIT
