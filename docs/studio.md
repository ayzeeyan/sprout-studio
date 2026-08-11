# Sprout Studio

Phase 8 preserves the end-to-end Windows project workflow, embedded Python execution, and validated external toolchain settings while adding the optional offline Academy. The x64 WinUI 3 application can create and save individual `.spr` files, create a project from any syntax template, validate and open `sprout.toml`, browse project sources, save edits, check and run through the real Rust engine, execute explicit Python imports, inspect Rust/Go installations, learn across six tracks, and reopen recent projects.

## Workflow

1. Start `SproutStudio.exe`.
2. Select **New project**, enter a name and template, and choose its parent directory; or select **Open project** and choose `sprout.toml`.
3. Select a `.spr` source in **Workspace**, edit it, and select **Save**. Check and Run also save an opened source before invoking the engine.
4. Select **Check** to validate without executing, or **Run** to execute.
5. Read program output in **Console** and structured diagnostics in **Problems**. Recent projects remain available in the left pane.
6. Select **Toolchains** to validate Rust and Go, recheck the environment, return to automatic detection, or save a custom executable/folder.
7. Select **Academy** to create a local learning profile, follow an explained recommendation, enroll in a track, read or complete lessons, compare concepts across languages, and review progress.

Selecting a problem moves the editor selection to its source span, including when non-ASCII text precedes the error. Engine work runs asynchronously so execution does not block the WinUI thread.

## Python project context

When Studio runs a source from an open project, it passes the validated project root through the native API. Rust resolves Python packages from that project's `.sprout/python` directory and cleans the project modules from CPython's process-wide import cache after execution. Use `sprout python sync <project>` to install dependencies before opening or running a project in Studio; a graphical Package Hub remains a Phase 9 feature.

The application output contains CPython and pip beside `SproutStudio.exe`; users do not need a system Python installation for supported interoperability. An installer and the remaining planned CLI commands are still deferred, and no disabled or placeholder action is exposed as complete.

## Toolchain settings

Toolchain checks do not run just because Studio launches. Opening **Toolchains** starts Rust and Go validation asynchronously and keeps the window responsive. Each card reports an exact state, detected version, executable path, validated components, capabilities, and an understandable failure message.

**Locate executable** and **Locate folder** save a custom path under the user's local application data. A configured path is authoritative, so a broken selection is reported instead of being silently replaced by another PATH result. **Auto detect** clears that setting and searches PATH and common Windows locations again. Toolchain processes are invoked without a shell and are cancellable when the settings dialog closes.

## Academy

Academy opens only when selected and can always be closed to return to the IDE. First use collects a small local-only profile; the **Profile** tab can later change goals, experience, and whether XP/achievement elements are shown. Course completion remains available even when gamification is minimized.

The **Learn** tab shows the exact rules behind recommendations, six tracks, complete offline lesson text, enrollment, and idempotent completion controls. Rust Systems and Go Backend validate their external toolchains at enrollment time and expose recovery actions if validation fails. The **Concepts** tab searches linked Sprout, Rust, Go, and Python examples. The **Progress** tab projects course completion, XP, levels, and meaningful achievements from distinct completed lessons. See [academy.md](academy.md) for persistence, privacy, and rule details.
