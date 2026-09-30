# Phase 9 Architecture

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

`sprout-core` owns the language pipeline, formatter, cursor-sensitive language service, recorded debugger, and Canvas command protocol. It has no dependency on projects, the CLI, C#, Avalonia, PyO3, or external crates. Its host-neutral interop traits describe foreign values, objects, errors, and imports. `sprout-python` implements those traits with embedded CPython. `sprout-project` owns versioned manifest parsing, validation, comment-preserving dependency updates, source discovery, templates, and validated interpreted bundles. `sprout-cli` is a thin adapter over shared APIs. `sprout-ffi` exposes those services to Studio as owned UTF-8 JSON responses. The runtime never evaluates generated Python or host-language source.

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

The .NET 10/Avalonia 12 executable depends on five UI-independent .NET libraries. `SproutStudio.Core` owns durable request/response models, `SproutStudio.Editor` owns multi-tab documents, atomic saves, workspace scanning, and language capabilities, `SproutStudio.Interop` owns native loading and deserialization, `SproutStudio.Toolchains` owns external-tool detection and safe process execution, and `SproutStudio.Academy` owns offline learning content and state. The UI passes structured requests to these services and renders results after returning to the UI thread.

The Avalonia shell is split into reusable `Shell`, `Workspace`, `Editor`, `Tools`, and `Inspector` controls instead of embedding every region in `MainWindow.axaml`. `MainWindowViewModel` coordinates existing services and shared commands while view code remains limited to desktop interaction and editor presentation. Shared colors, brushes, spacing, radii, and compact control styles live in one theme dictionary. `StudioLayoutStateStore` remains in the UI-independent editor library so persistence, corruption fallback, and size clamping are integration-tested without starting Avalonia.

The activity rail selects a Studio mode inside that same shell. Workspace remains alive while Search, Source Control, Academy, Documentation, Packages, or Toolchains is active, so open documents and editor state are not reconstructed during navigation. Mode controls are created on first use and then reused. Project Search performs a bounded on-demand filesystem walk and ignores generated/dependency directories; it does not build a startup index.

Documentation keeps Markdown files as the source of truth. Markdig parses only the active page into an AST, and an Avalonia-native renderer produces headings, emphasis, links, lists, quotes, tables, and copyable code blocks without a browser or WebView. The catalog retains lightweight source text for local title, heading, and excerpt search. Source Control invokes the installed Git executable through `ProcessRunner` argument lists and parses porcelain v2 status into branch, upstream, ahead/behind, staged, unstaged, untracked, and rename models. Stage, unstage, diff, commit, fetch, fast-forward pull, and push use the same safe process boundary; Studio stores no Git credentials.

The FFI exposes coarse versioned operations for execution/debugging, language inspection/formatting, project load/create/dependency updates, and bundle builds. Panics are contained at the ABI boundary and returned buffers use the matching Rust allocator. C# never parses console text to recover language diagnostics; spans, suggestions, fixes, symbols, variables, stack frames, Canvas commands, artifacts, and project issues remain structured end to end. Rust byte offsets are converted to UTF-16 indices before AvaloniaEdit navigation.

## Toolchain system

External-language toolchains do not affect Sprout startup and are checked only when integrated Toolchains mode, Academy enrollment, or a toolchain operation requests them. `ToolchainRegistry` holds independent language adapters. `ToolchainManager` combines those adapters with persisted custom paths and a shared `ProcessRunner`.

Detection considers an authoritative user-selected location, PATH, and known Windows installation locations. Finding a file is not treated as validation: Rust executes `rustc --version`, `cargo --version`, and `rustfmt --version`; Go executes `go version` and a bounded `gofmt -h` probe. Results distinguish ready, not installed, found but broken, partial installation, unsupported version, and a validated custom location. Rust 1.95 and Go 1.22 are the current minimum supported versions.

Process requests carry an executable, an argument list, an optional working directory, and an optional timeout. `UseShellExecute` is disabled and each argument is passed through `ProcessStartInfo.ArgumentList`; user strings are never concatenated into shell commands. stdout, stderr, exit code, duration, start failure, and timeout state are captured. Cancellation and timeout terminate the complete child process tree without blocking the Avalonia UI thread.

Custom locations are stored in a versioned JSON document under local application data and replaced atomically. Rust and Go documents route Run, Build, Format, and Test through these validated adapters. Academy additionally uses the Rust, Go, Python, and Java adapters for isolated graded activities. Python, Java, JavaScript, TypeScript, JSON, TOML, Markdown, and plain-text files can be opened, edited, tabbed, and saved in the general workspace, but Studio disables general-purpose semantic or execution commands it does not implement.

## Academy system

Academy content is authored in `academy/catalog.json`, embedded as an offline resource, and validated for unique identifiers and valid track/lesson/concept relationships when loaded. Academy is a lazy integrated Studio mode rather than a second window. Avalonia does not own track rules or lesson semantics. `AcademyService` coordinates the catalog, atomic local state store, recommendation engine, and `ToolchainManager`.

The local state contains a display name, preferred username, experience level, goals, gamification preference, enrollments, distinct completed activity identifiers, official submission records, hint usage, and achievement timestamps. XP, levels, course completion, and earned achievements are projections of that durable state. Passing an existing activity is idempotent and awards zero additional XP.

Recommendations are ordered rules with user-visible explanations. Beginners receive Sprout fundamentals before their goal track; intermediate and advanced users receive their selected goal tracks directly. No AI capability is claimed. Cross-language concepts use linked Sprout, Rust, Go, and Python examples so the content teaches transferable ideas rather than isolated syntax.

Rust, Go, and Java enrollment invokes validated detection only for tracks that require those external tools. Failed enrollment preserves the exact ready/missing/broken/partial/unsupported/custom state and offers auto-detection, persisted location selection, offline setup guidance, retry, and cancel.

Academy activities open as native editor tabs rather than a separate window. Coding lessons link to a persistent companion source file and use a responsive lesson/editor split; quizzes and checkpoints occupy a single document surface. Sandbox **Run Tests** and official **Submit** share the same structured grading path, but only Submit atomically appends an attempt and awards completion, XP, and achievements. Java lessons compile and run an isolated generated harness, Rust/Go/Python exercises use their validated adapters, and Sprout exercises use the native engine. Runtime tests, output comparisons, compiler diagnostics, expected/observed values, durations, and source-navigation hints remain structured through the Tests panel and lesson checklist. Module checkpoints remain locked until their module lessons are complete. Hint state preserves four progressive tiers with a local-calendar-day allowance and free rereads.

## Explicitly deferred

- Fields, constructors with parameters, inheritance, Sprout collection literals, native Sprout modules/imports, and Sprout error handling
- Python keyword arguments, assignment through Python indexes/members, and compatibility guarantees for arbitrary PyPI packages
- Remaining CLI commands (`test`, `fmt`, `repl`, `add`, and `build`)
- General Sprout package resolution/registry infrastructure, native executables, bytecode, and VM
- Live pause/resume debugging, conditional breakpoints, rename refactoring, rich syntax coloring, and language servers for non-Sprout files
- Academy certificates, cloud profile synchronization, and automated toolchain installation

No deferred command or feature is exposed as if it works.
