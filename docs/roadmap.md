# Roadmap to 1.0

Sprout Studio 0.2.0 is the last alpha. The next release line leaves preview: **0.3 Beta** completes the feature set, **0.9 Release Candidate** freezes it, and **1.0.0** is the first stable release. This page is the plan for getting there and the bar 1.0 has to clear.

## What "1.0" promises

1.0 is a promise to learners and teachers, not a feature count. A release is 1.0 only when all of these hold:

- **Your work is safe.** No release loses source files, unsaved edits, settings, or Academy progress. Every upgrade from 0.1.0 onward migrates data automatically, and a crash restores unsaved editors on the next start.
- **The language is stable.** Programs that run on Sprout 1.0 keep running on every 1.x release. Breaking changes wait for 2.0 and come with a migration guide.
- **The installer is trustworthy.** Setup and the application are code-signed, checksums are published, and Windows SmartScreen does not block a normal install.
- **It works for everyone.** Every screen can be used with a keyboard and a screen reader, text meets WCAG AA contrast in every theme, and the UI scales cleanly from 100% to 200%.
- **It is fast.** Studio is interactive in under 2 seconds on a typical laptop, typing never lags in a 10,000-line file, and the Academy runs an exercise in under a second.
- **It is documented.** Every feature in Studio has a Handbook guide or a reference page, and every diagnostic code links to an explanation.

## Language and engine

| Feature | Why it matters | Milestone |
| --- | --- | --- |
| Sprout 1.0 language specification | One written definition of Guided, Structured, and Advanced syntax that the engine is tested against. | 0.9 |
| Enforced type annotations | Structured and Advanced profiles check the types learners write instead of only parsing them. | 0.3 |
| Diagnostics with fixes | Common mistakes (missing `end`, misspelled names, wrong indentation) offer a one-click fix and a link to the guide that explains them. | 0.3 |
| Standard library 1.0 | Stable `text`, `math`, `random`, `time`, `files`, `json`, and `collections` modules with documented, versioned APIs. | 0.9 |
| Native `sprout build` | Build a project into a standalone Windows executable, from both the CLI and Studio, instead of an interpreted bundle. | 0.3 |
| Live debugger | Pause a running program at breakpoints and step through it live, alongside today's recorded time-travel debugging. | 0.3 |

## Studio

| Feature | Why it matters | Milestone |
| --- | --- | --- |
| Hot exit and crash recovery | Unsaved editors and layout come back after a crash or restart. The editor already has a session recovery service; Studio needs to save to it and offer a restore on startup. | 0.3 |
| Project-wide find and replace | Today's replace works in one file; Search gains replace across files with a preview of every change. | 0.3 |
| Rename symbol and quick fixes | Engine-backed rename and code actions for Sprout. | 0.3 |
| Light and high-contrast themes | Readable in bright classrooms and for learners with low vision. | 0.3 |
| Keyboard shortcut editor | View, search, and remap every command. | 0.9 |
| In-app updates | Download, verify against `SHA256SUMS.txt`, and install a new release from **About** without visiting GitHub. | 0.3 |
| Integrated terminal tabs | Several terminals per project, each with its own working directory. | 0.9 |
| First-run tour | A short guided tour of the editor, Academy, and Learn inspector for new users. | 0.3 |

## Academy

| Feature | Why it matters | Milestone |
| --- | --- | --- |
| Complete Sprout curriculum | Four tracks (Foundations, Data, Objects, Projects) with about 40 lessons, so learners can go from first program to a finished app. | 0.9 |
| Capstone projects | Multi-file projects, such as a text adventure, a quiz game, or a Canvas drawing app, with requirements checked by the engine. | 0.9 |
| Checkpoint quizzes | Short concept checks between lessons that feed the streak and daily goal. | 0.3 |
| Explain this error | Every diagnostic in the editor links to the Handbook section that explains it. | 0.3 |
| Progressive hints | Hints that reveal step by step, from a nudge to a worked example, within the daily hint budget. | 0.3 |
| Progress backup | Export and import Academy progress as a single file, so learners can move to a new PC. | 0.3 |
| Classroom packs | Teachers can install a custom track from a folder or `.sproutpack` file without rebuilding Studio. | 0.9 |
| Handbook coverage | A guide for every concept the curriculum teaches, each with runnable, test-verified samples. | 0.9 |

## Quality and release engineering

- **Pull-request CI**: run formatting, Clippy, Rust tests, .NET tests, and the Handbook sample checks on every pull request, not only on release tags.
- **UI regression tests**: promote the headless screenshot harness used for 0.2.0 into the test suite, so layout overlaps and clipped buttons fail the build.
- **Code signing**: sign Setup, `SproutStudio.exe`, `sprout.exe`, and `sprout_ffi.dll` in the release workflow (the pipeline already supports `SPROUT_SIGN_CERT_SHA1`).
- **winget package**: publish `SproutStudio.SproutStudio` to the Windows Package Manager on each stable release.
- **Accessibility audit**: automation names on every control, focus order checks, and a Narrator pass of each mode.
- **Performance budgets**: startup, typing latency, and exercise run time measured in CI against the targets above.
- **Local crash reports**: unhandled exceptions are written to `%LOCALAPPDATA%\SproutStudio\Logs` with a one-click "copy report" in **About**. Nothing is sent anywhere automatically.

## Not planned for 1.0

These are good ideas that would delay a solid 1.0, so they wait for 1.x or later:

- macOS, Linux, and Windows on ARM builds (the engine is portable, the installer and toolchain adapters are not yet).
- Cloud accounts or progress sync. Progress stays local and private, with export and import for moving it.
- Online package registry for Sprout libraries.
- AI code completion or chat inside the Academy.

## Milestones

| Release | Goal | Exit criteria |
| --- | --- | --- |
| **0.3.0 Beta** | Feature complete for 1.0. | Every 0.3 item above is shipped; pull-request CI is required; no known data-loss bugs. |
| **0.9.0 Release Candidate** | Freeze and polish. | Language specification and standard library frozen; full curriculum; accessibility and performance budgets met; signed installer. |
| **1.0.0** | Stable. | Two weeks of RC use with no release-blocking issues, and every promise in "What 1.0 promises" holds. |

Progress against this plan is tracked in GitHub issues with the `1.0` milestone. Suggestions are welcome: open an issue describing what you'd like to learn or build with Sprout.
