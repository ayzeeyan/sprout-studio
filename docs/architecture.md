# Phase 6 Architecture

## Implemented pipeline

```text
Guided / Structured / Advanced source
    -> profile lexer rules (tokens and exact source spans)
    -> profile parser
    -> canonical AST
    -> semantic analyzer (scopes, symbols, types, members)
    -> tree-walking runtime
       -> explicit embedded-CPython interop when requested by the AST
    -> captured program output or structured diagnostic
```

`sprout-core` owns the language pipeline and has no dependency on projects, the CLI, C#, WinUI, PyO3, or external crates. Its host-neutral interop traits describe foreign values, objects, errors, and imports. `sprout-python` implements those traits with embedded CPython. `sprout-project` owns versioned manifest parsing, validation, source discovery, and built-in templates. `sprout-cli` is a thin adapter over those shared APIs. `sprout-ffi` exposes the language and project services to Studio as owned UTF-8 JSON responses. The runtime never evaluates generated Python or host-language source.

The canonical AST is independent of surface syntax. Guided, Structured, and Advanced parser paths produce the same node types without translating text between profiles. Advanced indentation is converted into structural `Indent`/`Dedent` tokens before parsing. Every source token and AST node carries a `SourceSpan` with file, byte offsets, and one-based line/column coordinates.

`Program::semantically_equivalent` compares canonical structure while intentionally ignoring profile-specific source spans. Cross-profile tests guarantee that equivalent declarations, functions, compound assignments, loops, branches, classes, member calls, returns, and expressions converge on the same meaning.

Compiler stages remain separate Rust modules with explicit data boundaries inside `sprout-core`. Project behavior is a separate crate because both CLI and Studio consume it and it has its own manifest and filesystem concerns.

## Project system

`sprout.toml` begins with `manifest_version = 1`. Loading rejects unsupported versions, malformed names or semantic versions, unknown syntax profiles, unsafe entry paths, missing entries, invalid dependency requirements, and entries that resolve outside the project root. Validation issues use stable `SPP` codes and cross the FFI as structured data.

Templates are written to a temporary sibling and renamed into place only after every file succeeds, so failed creation does not present a partial project as valid. A generated project contains `src/main.spr`, an isolated `.sprout/python` environment directory, and a `.gitignore` rule that prevents local dependency state from being committed. Source discovery does not follow directory symlinks, and the canonical entry path must remain inside the project root.

CLI `run` and `check` load the manifest and use its entry and syntax profile. Studio calls the same Rust loader through FFI, displays discovered sources in its project explorer, and persists at most ten de-duplicated recent projects in local application data.

## Python interoperability

`import python "module" as alias` is a real canonical AST statement accepted by all three syntax frontends. Semantic analysis validates the module name, declares the alias as a dynamic foreign value, and retains normal source spans. Only execution initializes CPython; `check` remains side-effect free.

The runtime converts primitive values directly and small lists or string-key maps recursively. Everything else uses a host-neutral object proxy. CPython proxies forward member lookup, calls, display, and indexing through PyO3 while translating Python exceptions into `SPT301` diagnostics. Large collections remain proxied so the boundary does not perform unbounded eager copies.

Each project owns `.sprout/python`. The package synchronizer runs pinned bundled Python and pip with structured process arguments and `--target`; it never invokes a shell or changes global/user site-packages. Execution temporarily adds the validated project's package path and removes project-originated modules from CPython's global module cache when the host is dropped, preventing sequential projects from reusing one another's modules.

The Windows build uses the official CPython 3.13 x64 embeddable distribution and an ABI3 PyO3 build. Both the CPython archive and pip wheel are version/hash pinned. Studio and packaged CLI outputs copy the runtime beside the executable, so supported end users do not need a system Python installation.

## Error model

Lexer, parser, semantic, and runtime failures produce `Diagnostic` values containing a stable code, severity, title, message, source span, related locations, suggestions, and optional replacement fixes. Invalid user source returns diagnostics and a nonzero CLI status rather than causing a Rust panic. Suggestions and fixes are selected only from symbols or members visible in the diagnosed context.

## Semantic system

The analyzer walks the canonical AST before execution. It maintains lexical scopes and symbol tables, infers basic expression and variable types, validates assignment compatibility, operators, conditions, repeat counts, function arity, return placement and consistency, classes, and member access. Semantic failure prevents execution, so a rejected program cannot produce partial side effects.

Classes currently support zero-argument construction and actions. A bound method receives `self` in its lexical scope. Unknown-member diagnostics resolve the receiver type, search only that class's real actions, and attach suggestions when edit distance indicates a credible match.

## Current runtime

The runtime is a tree-walking AST interpreter with nested lexical environments. It supports variables, assignment, expressions, conditionals, repeat/while loops, actions, returns, calls, basic classes and bound methods, indexing, explicit Python imports, and the native `print` action. Python can supply list and map runtime values even though Sprout collection literal syntax is not implemented yet. Integer arithmetic is checked for overflow.

## Studio boundary

The WinUI 3 executable depends on two UI-independent .NET libraries. `SproutStudio.Core` owns document state and session orchestration, while `SproutStudio.Interop` owns native library loading and response deserialization. The UI passes structured requests to the Rust FFI on a worker thread and renders output or diagnostics after returning to the UI thread.

The FFI exposes a small versioned surface: execute a structured request with optional project context, load or create a project, inspect the returned buffer, and free it with the matching Rust allocator. Panics are contained at the ABI boundary. C# never parses console text to recover diagnostics; source spans, related locations, suggestions, fixes, and project issues remain structured end to end. Rust byte offsets are converted to UTF-16 editor indices before WinUI navigation.

## Explicitly deferred

- Fields, constructors with parameters, inheritance, Sprout collection literals, native Sprout modules/imports, and Sprout error handling
- Python keyword arguments, assignment through Python indexes/members, and compatibility guarantees for arbitrary PyPI packages
- Remaining CLI commands (`test`, `fmt`, `repl`, `add`, and `build`)
- Graphical Package Hub, general Sprout package infrastructure, bytecode, and VM

No deferred command or feature is exposed as if it works.
