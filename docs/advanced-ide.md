# Phase 9 Advanced IDE

## IDE shell and layout

The Studio shell is composed from focused Avalonia controls for the title bar, toolbar, activity bar, Explorer, AvaloniaEdit host, bottom tools, inspector, and status bar. A star-sized editor column receives remaining space while the Explorer, Inspector, and bottom panel use constrained drag splitters. The Inspector collapses before the Explorer as the window narrows, and hidden panes leave no empty shell behind.

Explorer width, Inspector width, bottom-panel height, pane visibility, selected tool tab, inspector tab, and activity destination are stored in `%LOCALAPPDATA%\SproutStudio\layout.json`. Invalid or obsolete values are clamped to safe limits, and a damaged file falls back to the default layout without preventing startup.

The toolbar contains file, project, syntax-profile, and execution commands only. Academy, documentation, real Git status, Packages, and toolchains live in the icon activity rail. At compact widths the labeled project group becomes two icon commands with tooltips instead of disappearing. All shell colors, brushes, spacing values, radii, and common compact-control styles come from `Styles/StudioTheme.axaml`.

`Ctrl+B` toggles the Explorer, `Ctrl+J` toggles bottom tools, and `Ctrl+Shift+P` opens the searchable command palette. The palette, toolbar, activity rail, and keyboard shortcuts invoke the same command registry so command availability and behavior remain consistent.

## Integrated Studio modes

Workspace, Search, Source Control, Academy, Documentation, Packages, and Toolchains replace the primary content inside one `MainWindow`. Workspace visuals remain attached and retain documents, caret state, pane sizes, and selected tools while another mode is active. Each secondary mode is instantiated only on first use and refreshes when the current project changes.

Project Search is bounded, cancellable, supports case/whole-word/file filters, and opens a selected result at its file and line. Documentation uses Markdig plus native Avalonia controls for rendered offline Markdown, per-page contents, local search, code copying, source opening, and Academy links. Source Control uses Git porcelain v2 and structured process arguments for repository state, unified diffs, staging, unstaging, commits, fetch, fast-forward pull, and push. A failed push after a successful commit is reported as that exact partial outcome.

`LanguageRegistry` is the shared identity source for Sprout, Rust, Go, Python, Java, C, C++, C#, JavaScript, TypeScript, and document formats. Compact local glyph tiles reuse its names, accents, extensions, toolchains, documentation links, and Academy-track links in the editor, Explorer, Academy, Toolchains, and project creation surfaces.

## Sprout language service

Completion, hover information, definition lookup, outline symbols, and formatting are Rust services over the canonical AST and lexer. Avalonia sends source, syntax profile, source name, and a UTF-8 cursor offset through the versioned FFI. Partial-source completion falls back to real lexer tokens; the GUI contains no Sprout grammar rules. Formatting refuses comment-bearing source until comments can be round-tripped safely.

The editor maps returned UTF-8 spans through normalized UTF-16 coordinates. Diagnostics and navigation therefore remain accurate across lines and after non-ASCII source.

## Recorded debugger and watches

Debug runs execute the same interpreter while recording a snapshot after real statements. Each snapshot contains its source span, visible lexical variables, call stack, output so far, and Canvas state. The debugger panel steps backward or forward through that immutable trace. A line breakpoint selects a matching snapshot after the run; it is not presented as live process suspension. Watch names prioritize matching variables and explicitly show when a name is out of scope.

## Canvas protocol

The runtime exposes `canvas_clear(color)`, `canvas_rect(x, y, width, height, color)`, and `canvas_circle(x, y, radius, color)`. They emit host-neutral commands. Rust never references Avalonia; the Studio Canvas control renders those commands after Run or while reviewing a debug snapshot.

## Packages and builds

Package Hub is a graphical, validated manifest editor for Sprout and Python dependency tables. It does not invent a remote Sprout registry or silently install packages. Python environment synchronization remains the explicit safe `sprout python sync` workflow.

Build validates the project entry and creates a unique interpreted `.sproutbundle` under the project's `build` directory. Bundles include their manifest, discovered source files, and an explicit runtime requirement. Native compilation is not implemented.

## Additional-language contract

All supported files share multi-tab open/edit/save and atomic persistence. Rust, Go, Python, and Java expose only the actions their validated external adapters support, using structured arguments, captured output, cancellation, timeouts, and process-tree termination. JavaScript, TypeScript, JSON, TOML, Markdown, and text remain honest edit/save adapters; semantic and execution controls are disabled for them.
