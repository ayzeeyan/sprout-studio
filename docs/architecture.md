# Phase 1 Architecture

## Implemented pipeline

```text
Guided source
    -> lexer (tokens and exact source spans)
    -> Guided parser
    -> canonical AST
    -> tree-walking runtime
    -> captured program output or structured diagnostic
```

`sprout-core` owns the language pipeline and has no dependency on the CLI, C#, WinUI, or external crates. `sprout-cli` is a thin adapter that reads source, calls high-level engine APIs, renders diagnostics, and selects an exit code. The runtime never evaluates generated Python or host-language source.

The canonical AST is intentionally independent of Guided syntax. Future Structured and Advanced parsers must produce the same node types instead of translating text between profiles. Every token and AST node carries a `SourceSpan` with file, byte offsets, and one-based line/column coordinates.

The current workspace uses two crates rather than one crate per compiler stage. Compiler stages remain separate Rust modules with explicit data boundaries. They can be extracted when independent versioning, compile-time isolation, or reuse provides a concrete benefit; doing so in Phase 1 would add packaging overhead without improving the boundaries.

## Error model

Lexer, parser, and runtime failures produce `Diagnostic` values containing a stable code, severity, title, message, source span, and suggestions collection. Invalid user source returns diagnostics and a nonzero CLI status rather than causing a Rust panic. Phase 2 will add semantic diagnostics and context-derived name suggestions.

## Current runtime

The Phase 1 runtime is a tree-walking AST interpreter with nested lexical environments. It supports variables, assignment, expressions, conditionals, repeat/while loops, actions, returns, calls, and the native `print` action. Integer arithmetic is checked for overflow.

## Explicitly deferred

- Semantic analysis, symbols, types, and duplicate-declaration checks (Phase 2)
- Structured and Advanced syntax frontends and AST equivalence tests (Phase 3)
- WinUI Studio and Rust/C# FFI (Phase 4)
- Project manifests and remaining CLI commands (Phase 5)
- Lists, maps, objects/classes, modules/imports, and error handling
- Python interoperability, package infrastructure, bytecode, and VM

No deferred command or feature is exposed as if it works.

