# Phase 3 Architecture

## Implemented pipeline

```text
Guided / Structured / Advanced source
    -> profile lexer rules (tokens and exact source spans)
    -> profile parser
    -> canonical AST
    -> semantic analyzer (scopes, symbols, types, members)
    -> tree-walking runtime
    -> captured program output or structured diagnostic
```

`sprout-core` owns the language pipeline and has no dependency on the CLI, C#, WinUI, or external crates. `sprout-cli` is a thin adapter that reads source, calls high-level engine APIs, renders diagnostics, and selects an exit code. The runtime never evaluates generated Python or host-language source.

The canonical AST is independent of surface syntax. Guided, Structured, and Advanced parser paths produce the same node types without translating text between profiles. Advanced indentation is converted into structural `Indent`/`Dedent` tokens before parsing. Every source token and AST node carries a `SourceSpan` with file, byte offsets, and one-based line/column coordinates.

`Program::semantically_equivalent` compares canonical structure while intentionally ignoring profile-specific source spans. Cross-profile tests use it to guarantee that equivalent declarations, functions, compound assignments, loops, calls, and expressions converge on the same meaning.

The current workspace uses two crates rather than one crate per compiler stage. Compiler stages remain separate Rust modules with explicit data boundaries. They can be extracted when independent versioning, compile-time isolation, or reuse provides a concrete benefit; doing so now would add packaging overhead without improving the boundaries.

## Error model

Lexer, parser, semantic, and runtime failures produce `Diagnostic` values containing a stable code, severity, title, message, source span, and suggestions collection. Invalid user source returns diagnostics and a nonzero CLI status rather than causing a Rust panic. Suggestions are selected only from symbols or members visible in the diagnosed context.

## Semantic system

The analyzer walks the canonical AST before execution. It maintains lexical scopes and symbol tables, infers basic expression and variable types, validates assignment compatibility, operators, conditions, repeat counts, function arity, return placement and consistency, classes, and member access. Semantic failure prevents execution, so a rejected program cannot produce partial side effects.

Classes currently support zero-argument construction and actions. A bound method receives `self` in its lexical scope. Unknown-member diagnostics resolve the receiver type, search only that class's real actions, and attach suggestions when edit distance indicates a credible match.

## Current runtime

The runtime is a tree-walking AST interpreter with nested lexical environments. It supports variables, assignment, expressions, conditionals, repeat/while loops, actions, returns, calls, basic classes and bound methods, and the native `print` action. Integer arithmetic is checked for overflow.

## Explicitly deferred

- WinUI Studio and Rust/C# FFI (Phase 4)
- Project manifests and remaining CLI commands (Phase 5)
- Fields, constructors with parameters, inheritance, lists, maps, modules/imports, and error handling
- Python interoperability, package infrastructure, bytecode, and VM

No deferred command or feature is exposed as if it works.
