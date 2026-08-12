# Sprout Studio

Phase 9 presents the x64 WinUI 3 application as a responsive dark IDE with an activity rail, project explorer, multi-tab editor, bottom output tools, and an optional right inspector. Sprout files use the real Rust engine for completion, hover information, outline, definition lookup, formatting, checking, running, recorded debugging, variables, watches, call stacks, and Canvas rendering.

## Workflow

1. Start `SproutStudio.exe`.
2. Select **New project**, enter a name and template, and choose its parent directory; or select **Open project** and choose `sprout.toml`.
3. Open any supported source file in a tab, edit it, and select **Save**. Sprout, Rust, Go, Python, Java, JavaScript, TypeScript, JSON, TOML, Markdown, and text extensions are recognized.
4. In Sprout files, use **Ctrl+Space** for completion, **F12** for definition lookup, **Format**, **Check**, **Run**, or **Debug**. The debugger records actual execution snapshots; its controls review the trace rather than claiming a live paused process.
5. Read structured diagnostics in **Problems**, output in **Console**, artifacts in **Build Output**, and graphics in **Canvas**. A breakpoint selects the matching recorded snapshot when one exists.
6. Select **Package Hub** to add, update, or remove validated `[dependencies]` or `[python]` entries without discarding manifest comments. Python installation remains the explicit `sprout python sync` operation.
7. Select **Build** in an open Sprout project to create a checked `.sproutbundle`. The UI identifies it as an interpreted bundle that requires a compatible runtime, not a native executable.
8. Rust and Go files route Run, Build, and Format to validated toolchain adapters. Other languages remain honest edit/save adapters with unsupported commands disabled.
9. Use the activity rail for Explorer, search, source control, Academy, documentation, Package Hub, and toolchains.

## Layout and keyboard workflow

Drag the dividers beside Explorer and Inspector or above the bottom tools to resize those panes. Use the pane header controls to hide Explorer, Inspector, or bottom tools; hidden panes do not reserve blank space. The shell remembers sizes, visibility, selected tabs, and the selected activity destination for the next launch.

- `Ctrl+Shift+P` opens the command palette.
- `Ctrl+B` toggles Explorer.
- `Ctrl+J` toggles the bottom tool panel.
- `Ctrl+N`, `Ctrl+O`, and `Ctrl+S` create, open, and save documents.
- `F5` runs the active supported document.
- `Ctrl+Space` requests Sprout completion and `F12` navigates to a definition.

At narrower widths the Inspector collapses first, then Explorer, so the editor remains usable. The toolbar moves lower-priority commands into its overflow menu rather than clipping them.

Selecting a problem moves the editor selection to its source span, including when non-ASCII text precedes the error. Engine work runs asynchronously so execution does not block the WinUI thread.

## Python project context

When Studio runs a source from an open project, it passes the validated project root through the native API. Rust resolves Python packages from that project's `.sprout/python` directory and cleans project modules from CPython's process-wide import cache after execution. Package Hub manages manifest declarations; use `sprout python sync <project>` to perform the cancellable, isolated installation workflow.

The application output contains CPython and pip beside `SproutStudio.exe`; users do not need a system Python installation for supported interoperability. An installer and the remaining planned CLI commands are still deferred, and no disabled or placeholder action is exposed as complete.

## Toolchain settings

Toolchain checks do not run just because Studio launches. Opening **Toolchains** starts Rust and Go validation asynchronously and keeps the window responsive. Each card reports an exact state, detected version, executable path, validated components, capabilities, and an understandable failure message.

**Locate executable** and **Locate folder** save a custom path under the user's local application data. A configured path is authoritative, so a broken selection is reported instead of being silently replaced by another PATH result. **Auto detect** clears that setting and searches PATH and common Windows locations again. Toolchain processes are invoked without a shell and are cancellable when the settings dialog closes.

## Academy

Academy opens only when selected and can always be closed to return to the IDE. First use collects a small local-only profile; the **Profile** tab can later change goals, experience, and whether XP/achievement elements are shown. Course completion remains available even when gamification is minimized.

The **Learn** tab shows the exact rules behind recommendations, six tracks, complete offline lesson text, enrollment, and idempotent completion controls. Rust Systems and Go Backend validate their external toolchains at enrollment time and expose recovery actions if validation fails. The **Concepts** tab searches linked Sprout, Rust, Go, and Python examples. The **Progress** tab projects course completion, XP, levels, and meaningful achievements from distinct completed lessons. See [academy.md](academy.md) for persistence, privacy, and rule details.
