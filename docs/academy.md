# Sprout Academy

Sprout Academy is an optional, offline learning surface inside Sprout Studio. It teaches transferable programming concepts and keeps the editor, project workflow, and native Sprout runtime available independently for experienced users.

## Local profile and privacy

Opening **Academy** for the first time asks only for:

- display name;
- preferred username;
- experience level: Absolute Beginner, Intermediate, or Advanced;
- one or more programming goals;
- whether to show XP, levels, and achievements.

The profile, enrollments, lesson completions, and achievement timestamps stay in a schema-versioned JSON document at:

```text
%LOCALAPPDATA%\SproutStudio\academy.json
```

Writes use a uniquely named temporary sibling and replacement, so partial content is not presented as valid state. No email, age, credentials, analytics identifier, or cloud account is collected. The profile can be edited from the Academy's **Profile** tab. Selecting Advanced initially minimizes gamification, but the user remains in control of that preference.

## Offline catalog

The catalog is stored separately from Avalonia in `academy/catalog.json` and embedded into the UI-independent `SproutStudio.Academy` library at build time. It contains seven working tracks. The Java course is the long-form path, with 22 coding lessons, six modules, recurring knowledge checks, and a checkpoint after every module; the other tracks provide focused three-lesson introductions.

| Track | External prerequisite |
|---|---|
| Sprout Programming Fundamentals | None |
| Sprout Game Development | None |
| Computer Science Foundations | None |
| Rust Systems Programming | Validated Rust 1.95+ |
| Go Backend Development | Validated Go 1.22+ |
| Python Automation | None; Sprout Studio already bundles Python |
| Java: Object Oriented Programming - Zero to Hero | Validated JDK |

Lesson content and programming concepts remain available without a network connection. The concept browser connects variables, functions, conditions, loops, collections, and data modeling across Sprout, Rust, Go, and Python examples. Search matches concept titles, explanations, and example languages.

## Recommendations

Recommendations are deterministic rules, not artificial intelligence. The Academy displays both the matching profile choice and the applied rule:

- Absolute Beginner adds Sprout Programming Fundamentals first.
- General Programming maps to Sprout Programming Fundamentals.
- Game Development maps to Sprout Game Development.
- Web / Backend maps to Go Backend Development.
- AI / Automation maps to Python Automation.
- Computer Science maps to Computer Science Foundations.
- Systems Programming maps to Rust Systems Programming.

Intermediate and Advanced profiles go directly to their selected goals; they are not forced through Sprout fundamentals. Duplicate results are removed while preserving learning-path order.

## Enrollment prerequisites

Sprout-only, computer-science, game, and Python tracks enroll immediately. Rust, Go, and Java are validated only when the user selects **Enroll** for the corresponding track. Merely starting Studio or opening Academy does not invoke an external toolchain.

If validation fails, the Academy shows the exact Phase 7 status and validated components, then provides working actions:

- **Auto Detect** clears a custom location and searches PATH and common locations again.
- **Locate Installation** validates and persists a selected installation folder.
- **Setup Instructions** reveals offline guidance without starting an installer.
- **Try Again** repeats enrollment using the current validated configuration.
- **Cancel** closes the prerequisite panel without enrolling.

The enrollment is persisted only after the required toolchain is usable. Automated external-tool installation is not implemented.

## Progress and achievements

Passing a graded lesson or checkpoint records its stable identifier once. Every official submission is stored with its attempt number, answers or code, requirement-level feedback, score, and timestamp; **Run Tests** never creates an attempt. Repeating a passing submission awards no additional XP, which prevents XP farming. Track completion is computed from distinct completed activities and remains visible even when gamification is hidden.

Current achievements reward meaningful milestones: first enrollment, first lesson, completing Sprout Fundamentals, beginning Python Automation, learning across two tracks, and completing an entire track. XP and levels are a projection of completed lesson values rather than a separately mutable counter.

## Integrated experience

Academy runs inside the main Studio shell and preserves the live Workspace when users switch modes. **Home** presents continue-learning, deterministic recommendations, projected progress, current toolchain readiness, and a queue that interleaves module checkpoints with lessons. **My Learning** lists enrolled tracks; **Learning Tracks** exposes module-grouped lessons, progress, prerequisites, enrollment, and completion; **Concept Browser** searches and compares linked language examples; **Challenges** lists remaining graded work; **Submissions** shows persisted official attempts; **Profile & Progress** edits the local profile and presents earned achievements.

Lessons, exercises, quizzes, and checkpoints open as native document tabs beside normal source files. Coding activities use a responsive split view with the rendered guide and requirement checklist on the left and a persistent real source document on the right. **Run Tests** performs a sandbox run and reports real timing and structured outcomes in the shared Tests panel without changing progress. **Submit** uses the same execution path, combines behavioral checks with any embedded knowledge questions, persists one official attempt, and awards completion and XP only after a passing result. Diagnostics retain expected and observed values and can navigate to relevant source.

Sprout activities execute through the authoritative native engine. Java compiles with `javac` and runs an isolated behavioral harness; Rust, Go, and Python activities build and run through their validated adapters. Every run uses a unique temporary directory and cleans it afterward. Four progressive hint tiers—Nudge, Concept, Structure, and Detail—are persisted separately; newly revealed hints consume the local-day allowance while rereading an unlocked hint is free. Academy links to relevant Documentation and Toolchains views through the shared Studio navigation system.

## Explicitly deferred

- Certificates
- Cloud accounts, profile synchronization, leaderboards, and analytics
- Automatic installation of Rust or Go
- Full project scaffolding for Academy activities; exercises intentionally use isolated single-activity workspaces

No deferred capability is exposed as complete.
