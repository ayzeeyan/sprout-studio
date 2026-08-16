# Toolchains

Sprout is bundled with Sprout Studio. Rust, Go, Python, and Java are external student toolchains and are detected only when a user opens integrated Toolchains mode, enrolls in a matching Academy track, or invokes a supported action from a matching editor tab.

## Status model

Every detection returns one of these explicit states:

- `READY`: all required components ran successfully and the version is supported.
- `NOT INSTALLED`: no candidate installation was found.
- `FOUND BUT BROKEN`: executables exist but a validation process failed, timed out, or returned unrecognized output.
- `PARTIAL INSTALLATION`: some required components are missing.
- `UNSUPPORTED VERSION`: the installation works but is older than Sprout Studio supports.
- `CUSTOM LOCATION · READY`: the persisted user-selected installation passed validation.

Sprout Studio currently requires Rust 1.95+, Go 1.22+, Python 3.10+, or JDK 17+ for their respective adapters.

## Detection and validation

Automatic detection searches PATH and common Windows locations. Rust validation requires working `rustc`, `cargo`, and `rustfmt` commands. Go validation requires working `go` and `gofmt`; Python probes `python`; Java requires `java` and `javac`. The adapters run bounded version/probe commands instead of assuming that an existing executable is healthy.

A saved custom path may identify the main executable, its `bin` directory, or an installation root containing `bin`. It is authoritative until **Auto detect** clears it. This preserves useful `FOUND BUT BROKEN`, `PARTIAL INSTALLATION`, and `UNSUPPORTED VERSION` feedback for the path the user actually selected.

Settings use schema-versioned JSON at:

```text
%LOCALAPPDATA%\SproutStudio\toolchains.json
```

Writes use a temporary sibling and atomic replacement so a cancelled or failed write does not present a partially written settings document.

## Capabilities

Validated adapters expose only real capabilities through `ToolchainManager`:

| Action | Rust | Go | Python | Java |
|---|---|---|---|---|
| Run | `cargo run` | `go run .` | `python file.py` | `java source.java` |
| Build/check | `cargo build` | `go build ./...` | `python -m py_compile` | `javac source.java` |
| Format | `cargo fmt` | `go fmt ./...` | — | — |
| Test | `cargo test` | `go test ./...` | `python -m unittest` | — |

Toolchains mode manages detection and paths, Academy reuses the same results for enrollment prerequisites, and matching editor tabs invoke these exact adapters. Studio finds the nearest language project marker when applicable; if no marker exists, it uses the file's parent. Unsupported language actions remain disabled rather than being simulated.

## Process safety

All processes use `UseShellExecute = false` and an argument list. The runner captures stdout, stderr, exit code, duration, start errors, and timeout state. Cancellation or timeout terminates the full child process tree. It never builds a shell command by concatenating user strings.

External compilers and programs are not sandboxed. They run with the permissions of the current Windows user.
