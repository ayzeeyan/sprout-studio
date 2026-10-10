# Sprout Studio 0.2.0 Alpha

Sprout Studio 0.2.0 Alpha turns Studio into an Academy IDE: learning is woven through the whole workspace, not tucked away in a separate mode. The editor explains the code you write, your progress follows you in the status bar, and the shell has been redesigned from the ground up.

## Learn while you code

- **Academy Handbook**: eleven short, beginner-friendly guides (the "easy version" of the documentation) covering first programs, decisions, loops, actions, classes, the standard library, syntax profiles, debugging, projects, and Python. Every guide switches to the **Full reference** documentation page with one click, tracks what you've read, links to the lessons that practice it, and every code sample is verified to run.
- **Read first**: each exercise lists the Handbook guides behind its concepts, and the start page suggests the guide for your next lesson.
- **Learn inspector**: a new first inspector tab detects the programming concepts used in the open Sprout, Python, Java, Rust, or Go file. It explains each one, shows the line where you used it, compares it with the same idea in other languages, and links straight to a lesson that teaches it.
- **Learning streaks and daily goals**: every Academy submission counts toward a daily streak, and first passes count toward a 30 XP daily goal. Streaks stay alive until midnight, so practicing yesterday still counts today.
- **Learning HUD**: the status bar shows your streak, daily-goal ring, and level everywhere in Studio. Select it to open your progress.
- **Learning hub**: the start page now leads with "Continue learning", showing your next lesson, streak, goal, and level.
- **Celebrations**: toasts celebrate completed lessons, level-ups, streaks, daily goals, and new achievements.
- **Progress profile**: a 12-week activity heatmap, current and longest streaks, and an achievement gallery that shows earned and locked badges.
- **New achievements**: Steady Growth (3-day streak), Evergreen (7-day streak), Persistent Gardener (pass after a failed attempt), Curious Mind (read a guide), and Well Read (read five guides). Reading a guide counts toward your streak.
- Learners who turn off "Show levels, XP, and achievements" in their profile see no streaks, levels, or XP celebrations.

## A redesigned IDE

- New layered dark theme with five accent themes (Sprout, Ocean, Amethyst, Ember, Rose) that recolor the shell instantly.
- Slim activity rail, title-bar command center, run toolbar capsule, pill tabs with unsaved-change dots, and a richer status bar with the engine version and architecture.
- New Sprout seedling logo, plus real Java, Go, Rust, and Python logos throughout Studio and Academy.
- Redesigned Settings, command palette, welcome page, and Academy tracks.

## Editor

- **Syntax highlighting for every language**: one consistent dark palette for Sprout, Python, Java, Rust, Go, C, C++, C#, JavaScript, TypeScript, JSON, TOML, and Markdown, in the editor, the Academy workspace, lessons, guides, documentation, the Concept Browser, and the Learn inspector.
- Tabs that share a file name show where each one lives, such as `main.spr  src` and `main.spr  Academy`.
- Toggle comment (`Ctrl+/`), duplicate line (`Ctrl+D`), move line (`Alt+Up/Down`), and go to line (`Ctrl+G`).
- Editor zoom (`Ctrl+=`, `Ctrl+-`, `Ctrl+0`, `Ctrl` + mouse wheel), current-line highlight, and indent guides.
- Zen mode (`F11`) and new shortcuts for Explorer, Search, Source Control, Problems, Documentation, and Settings.

## Academy workspace

- Exercise actions (Run Tests, Submit, and opening the exercise file) live in a footer that is always visible and never clipped.
- Lessons show their activity type, estimated time, XP, and position in the track; requirements read as plain-language goals.
- The Academy sidebar can be hidden (`Ctrl+B` or the title-bar toggle brings it back), shows the lesson you're practicing, and adds Handbook and Progress.
- The command palette can continue your lesson, open the Handbook or your progress, or open any guide by name.

## Fixes

- Toolchain checks allow a slow first start, so a healthy Rust, Go, Python, or Java installation is no longer reported as broken.
- The Academy sidebar's collapse button no longer leaves the Academy, and its add button now opens the track list.
- Toolchains no longer gets stuck on "The operation was canceled" when it is the first mode shown.
- Source Control no longer reports a null-reference error for projects that are not Git repositories.
- Selecting the Canvas inspector tab no longer jumps back to Properties.
- Progress bars, the title bar, welcome cards, and Academy layouts no longer overlap at narrow window sizes.

## Install

Run `SproutStudio-Setup-x64.exe`, or extract `SproutStudio-Portable-x64.zip` for a portable copy. This alpha is self-contained and targets Windows x64; it does not require a separate .NET runtime installation. Existing Academy progress and settings carry over from 0.1.0.

## Alpha Notice

This is an early alpha intended for testing and feedback. Back up important projects and report reproducible issues with the diagnostics information available from **About**.

---

## Sprout Studio 0.1.0 Alpha

Sprout Studio 0.1.0 Alpha is the first preview release of the Sprout desktop IDE for Windows. It combines a beginner-friendly Sprout workspace with real project tools, an integrated Academy, offline documentation, package and toolchain management, and the native Sprout engine.

### Highlights

- Guided and standard Sprout editing, checking, running, debugging, and building
- Project explorer, search, source control, terminal, REPL, diagnostics, and editor tooling
- Integrated Sprout Academy and offline documentation
- Local Go, Java, Python, and Rust toolchain detection
- Recent projects and About/Diagnostics system information
- Self-contained Windows x64 installer and portable archive

### Install

Run `SproutStudio-Setup-x64.exe`, or extract `SproutStudio-Portable-x64.zip` for a portable copy. This alpha is self-contained and does not require a separate .NET runtime installation.

### Alpha Notice

This is an early alpha intended for testing and feedback. Back up important projects and report reproducible issues with the diagnostics information available from **About**.
