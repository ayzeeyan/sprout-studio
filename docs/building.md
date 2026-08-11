# Building Sprout Phase 1

## Development requirements

- Windows, Linux, or macOS
- Rust 1.95 or a compatible newer stable toolchain

No runtime dependencies are required by the engine or CLI.

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

## Validate

```powershell
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo run -p sprout-cli -- run examples/hello.spr
```

The Rust toolchain is a development requirement only. A future Sprout Studio installer will bundle the engine and runtime so end users do not need Rust, Cargo, Visual Studio, or the .NET SDK.

