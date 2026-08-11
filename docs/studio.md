# Sprout Studio

Phase 5 provides an end-to-end Windows project workflow for Sprout. The x64 WinUI 3 application can create and save individual `.spr` files, create a project from any syntax template, validate and open `sprout.toml`, browse project sources, save edits, check and run through the real Rust engine, and reopen recent projects.

## Workflow

1. Start `SproutStudio.exe`.
2. Select **New project**, enter a name and template, and choose its parent directory; or select **Open project** and choose `sprout.toml`.
3. Select a `.spr` source in **Workspace**, edit it, and select **Save**. Check and Run also save an opened source before invoking the engine.
4. Select **Check** to validate without executing, or **Run** to execute.
5. Read program output in **Console** and structured diagnostics in **Problems**. Recent projects remain available in the left pane.

Selecting a problem moves the editor selection to its source span, including when non-ASCII text precedes the error. Engine work runs asynchronously so execution does not block the WinUI thread.

## Current boundary

This milestone is an unpackaged, self-contained development build. Python dependencies declared in a manifest are parsed and validated but are not installed until Phase 6. Installers and the remaining planned CLI commands are also deferred; disabled or placeholder actions for them are not exposed.
