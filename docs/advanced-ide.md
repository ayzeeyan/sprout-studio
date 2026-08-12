# Phase 9 Advanced IDE

## IDE shell and layout

The Studio shell is composed from focused WinUI controls for the title bar, toolbar, activity bar, Explorer, editor host, bottom tools, inspector, and status bar. A star-sized editor column receives remaining space while the Explorer, Inspector, and bottom panel use constrained drag splitters. The Inspector collapses before the Explorer as the window narrows, and hidden panes leave no empty shell behind.

Explorer width, Inspector width, bottom-panel height, pane visibility, selected tool tab, inspector tab, and activity destination are stored in `%LOCALAPPDATA%\SproutStudio\layout.json`. Invalid or obsolete values are clamped to safe limits, and a damaged file falls back to the default layout without preventing startup.

The toolbar contains file, project, syntax-profile, and execution commands only. Academy, documentation, source control, Package Hub, and toolchains live in the icon activity rail. All shell colors, brushes, spacing values, radii, and common compact-control styles come from `Themes/StudioTheme.xaml`.

`Ctrl+B` toggles the Explorer, `Ctrl+J` toggles bottom tools, and `Ctrl+Shift+P` opens the searchable command palette. The palette, toolbar, activity rail, and keyboard shortcuts invoke the same command registry so command availability and behavior remain consistent.

## Sprout language service

Completion, hover information, definition lookup, outline symbols, and formatting are Rust services over the canonical AST and lexer. WinUI sends source, syntax profile, source name, and a UTF-8 cursor offset through the versioned FFI. Partial-source completion falls back to real lexer tokens; the GUI contains no Sprout grammar rules. Formatting refuses comment-bearing source until comments can be round-tripped safely.

WinUI normalizes its carriage-return line breaks before requests and maps returned UTF-8 spans back through normalized UTF-16 coordinates. Diagnostics and navigation therefore remain accurate across lines and after non-ASCII source.

## Recorded debugger and watches

Debug runs execute the same interpreter while recording a snapshot after real statements. Each snapshot contains its source span, visible lexical variables, call stack, output so far, and Canvas state. The debugger panel steps backward or forward through that immutable trace. A line breakpoint selects a matching snapshot after the run; it is not presented as live process suspension. Watch names prioritize matching variables and explicitly show when a name is out of scope.

## Canvas protocol

The runtime exposes `canvas_clear(color)`, `canvas_rect(x, y, width, height, color)`, and `canvas_circle(x, y, radius, color)`. They emit host-neutral commands. Rust never references WinUI; the Studio adapter renders those commands with WinUI shapes after Run or while reviewing a debug snapshot.

## Packages and builds

Package Hub is a graphical, validated manifest editor for Sprout and Python dependency tables. It does not invent a remote Sprout registry or silently install packages. Python environment synchronization remains the explicit safe `sprout python sync` workflow.

Build validates the project entry and creates a unique interpreted `.sproutbundle` under the project's `build` directory. Bundles include their manifest, discovered source files, and an explicit runtime requirement. Native compilation is not implemented.

## Additional-language contract

All supported files share multi-tab open/edit/save and atomic persistence. Rust and Go expose Run, Build, Format, and Test capabilities through validated external toolchains with structured arguments, captured output, cancellation, timeouts, and process-tree termination. Python, Java, JavaScript, TypeScript, JSON, TOML, Markdown, and text are edit/save adapters in Phase 9; semantic and execution controls are disabled for them.
