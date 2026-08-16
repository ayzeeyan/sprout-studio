# Sprout Studio

Sprout Studio is a self-contained x64 .NET 10/Avalonia 12 application presented as a responsive dark IDE with an activity rail, project explorer, multi-tab editor, bottom output tools, and an optional right inspector. Sprout files use the real Rust engine for completion, hover information, outline, definition lookup, formatting, checking, running, recorded debugging, variables, call stacks, and Canvas rendering.

## Workflow

1. Start `SproutStudio.exe`.
2. Select **New project**, enter a name and template, and choose its parent directory; or select **Open project** and choose `sprout.toml`.
3. Open any supported source file in a tab, edit it, and select **Save**. Sprout, Rust, Go, Python, Java, JavaScript, TypeScript, JSON, TOML, Markdown, and text extensions are recognized.
4. In Sprout files, use **Ctrl+Space** for completion, **F12** for definition lookup, **Format**, **Check**, **Run**, or **Debug**. The debugger records actual execution snapshots; its controls review the trace rather than claiming a live paused process.
5. Read structured diagnostics in **Problems**, output in **Console**, artifacts in **Build Output**, and graphics in **Canvas**. A breakpoint selects the matching recorded snapshot when one exists.
6. Select **Packages** to add, update, or remove validated `[dependencies]` or `[python]` entries without discarding manifest comments. Python installation remains the explicit `sprout python sync` operation.
7. Select **Build** in an open Sprout project to create a checked `.sproutbundle`. The UI identifies it as an interpreted bundle that requires a compatible runtime, not a native executable.
8. Rust, Go, Python, and Java files route supported actions to validated toolchain adapters. Other languages remain honest edit/save adapters with unsupported commands disabled.
9. Use the activity rail to switch the same main window among Workspace, Search, Source Control, Academy, Documentation, Packages, and Toolchains. Returning to Workspace preserves the editor session.

The empty editor lists up to ten recently opened projects without scanning their contents at startup. Reopening a project moves it to the top; moved or deleted entries are identified in place and can be removed. **About** in the title bar reports the versions and build details read from the running Studio and native engine. **Copy System Info** copies those diagnostics, while **Open Logs Folder** opens Studio's stable local log directory.

## Layout and keyboard workflow

Drag the dividers beside Explorer and Inspector or above the bottom tools to resize those panes. Use the pane header controls to hide Explorer, Inspector, or bottom tools; hidden panes do not reserve blank space. The shell remembers sizes, visibility, selected tabs, and the selected activity destination for the next launch.

- `Ctrl+Shift+P` opens the command palette.
- `Ctrl+B` toggles Explorer.
- `Ctrl+J` toggles the bottom tool panel.
- `Ctrl+N`, `Ctrl+O`, and `Ctrl+S` create, open, and save documents.
- `Ctrl+W` closes the selected editor and `Ctrl+Tab` / `Ctrl+Shift+Tab` switch documents.
- `F5` runs the active supported document.
- `Ctrl+Shift+B` builds the active project or supported source.
- `Ctrl+Space` requests Sprout completion and `F12` navigates to a definition.

At narrower widths the Inspector collapses first so the editor remains usable. The toolbar replaces the labeled New/Open Project group with compact icon commands and tooltips rather than clipping or silently removing it.

Selecting a problem moves the editor selection to its source span, including when non-ASCII text precedes the error. Engine work runs asynchronously so execution does not block the Avalonia UI thread.

## Python project context

When Studio runs a source from an open project, it passes the validated project root through the native API. Rust resolves Python packages from that project's `.sprout/python` directory and cleans project modules from CPython's process-wide import cache after execution. Packages mode manages manifest declarations; use `sprout python sync <project>` to perform the cancellable, isolated installation workflow.

The application output contains the Sprout CLI, CPython, and pip beside `SproutStudio.exe`; users do not need a system Python installation for supported interoperability. The Setup executable installs that same deterministic self-contained publish, and no disabled or placeholder action is exposed as complete.

## Toolchain settings

Toolchain checks do not run just because Studio launches. Opening integrated **Toolchains** mode starts Rust, Go, Python, and Java validation asynchronously and keeps the window responsive. Each row reports the exact state, detected version, executable path, and an understandable failure message.

**Locate executable** and **Locate folder** save a custom path under the user's local application data. A configured path is authoritative, so a broken selection is reported instead of being silently replaced by another PATH result. **Auto detect** clears that setting and searches PATH and common Windows locations again. Toolchain processes are invoked without a shell and are cancellable when the settings dialog closes.

## Academy

Academy is created only when selected and replaces the primary content inside the existing Studio shell. Returning to Workspace restores the same documents and pane state. First use collects a small local-only profile; **Profile & Progress** can later change goals, experience, and whether XP/achievement elements are shown. Course completion remains available even when gamification is minimized.

Home, My Learning, Learning Tracks, Concept Browser, and Profile & Progress expose the exact rules behind recommendations, six tracks, complete offline lesson text, enrollment, idempotent completion, and meaningful achievements. Rust Systems and Go Backend validate their external toolchains at enrollment time and link to Toolchains for recovery. Sprout lessons can open a real practice document in Workspace without pretending that the scratch is automatically graded. See [academy.md](academy.md) for persistence, privacy, and rule details.

## Documentation and Source Control

Documentation renders the bundled Markdown corpus with native Avalonia controls. It provides contents, back/forward history, local search with excerpts, a heading table of contents, IDE-style code blocks with copy actions, source opening, and Academy links. No WebView or network connection is required.

Source Control detects the actual Git repository, shows branch/upstream and ahead/behind state, separates working and staged changes, renders a bounded unified diff, and supports stage/unstage, commit, fetch, fast-forward pull, push, and Commit & Push. All arguments pass through the structured process runner, and Studio delegates credentials to the installed Git infrastructure.
