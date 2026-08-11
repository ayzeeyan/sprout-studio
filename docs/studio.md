# Sprout Studio

Phase 4 provides the first end-to-end Windows desktop workflow for Sprout. The x64 WinUI 3 application can create an untitled document, open a `.spr` file, edit source, choose a syntax profile, check it, and run it through the real Rust engine.

## Workflow

1. Start `SproutStudio.exe`.
2. Select **New** or **Open** from the command bar.
3. Write Sprout in the editor and choose Guided, Structured, or Advanced.
4. Select **Check** to validate without executing, or **Run** to execute.
5. Read program output in **Console** and structured diagnostics in **Problems**.

Selecting a problem moves the editor selection to its source span. Engine work runs asynchronously so execution does not block the WinUI thread.

## Current boundary

This milestone is an unpackaged, self-contained development build. Project manifests, recent projects, templates, installers, and CLI project integration belong to Phase 5. Disabled or placeholder actions for those features are not exposed in the Phase 4 UI.
