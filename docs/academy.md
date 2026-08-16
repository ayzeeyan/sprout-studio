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

The catalog is stored separately from Avalonia in `academy/catalog.json` and embedded into the UI-independent `SproutStudio.Academy` library at build time. It contains six working tracks, each with three lessons:

| Track | External prerequisite |
|---|---|
| Sprout Programming Fundamentals | None |
| Sprout Game Development | None |
| Computer Science Foundations | None |
| Rust Systems Programming | Validated Rust 1.95+ |
| Go Backend Development | Validated Go 1.22+ |
| Python Automation | None; Sprout Studio already bundles Python |

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

Sprout-only, computer-science, game, and Python tracks enroll immediately. Rust and Go are validated only when the user selects **Enroll** for the corresponding track. Merely starting Studio or opening Academy does not invoke an external toolchain.

If validation fails, the Academy shows the exact Phase 7 status and validated components, then provides working actions:

- **Auto Detect** clears a custom location and searches PATH and common locations again.
- **Locate Installation** validates and persists a selected installation folder.
- **Setup Instructions** reveals offline guidance without starting an installer.
- **Try Again** repeats enrollment using the current validated configuration.
- **Cancel** closes the prerequisite panel without enrolling.

The enrollment is persisted only after the required toolchain is usable. Automated external-tool installation is not implemented.

## Progress and achievements

Completing a lesson records its stable identifier once. Repeating the action awards no additional XP, which prevents XP farming. Track completion is computed from distinct completed lessons and remains visible even when gamification is hidden.

Current achievements reward meaningful milestones: first enrollment, first lesson, completing Sprout Fundamentals, beginning Python Automation, learning across two tracks, and completing an entire track. XP and levels are a projection of completed lesson values rather than a separately mutable counter.

## Integrated experience

Academy runs inside the main Studio shell and preserves the live Workspace when users switch modes. **Home** presents continue-learning, deterministic recommendations, projected progress, and current toolchain readiness. **My Learning** lists enrolled tracks; **Learning Tracks** exposes module-grouped lessons, progress, prerequisites, enrollment, and completion; **Concept Browser** searches and compares linked language examples; **Profile & Progress** edits the local profile and presents earned achievements.

Sprout lessons can open a real runnable practice document in Workspace. The generated source contains the lesson objective and uses the authoritative Sprout engine when the user runs or checks it. This bridge is deliberately identified as practice, not as an automatically graded exercise. Academy links to relevant Documentation and Toolchains views through the shared Studio navigation system.

## Explicitly deferred

- Automatic exercise grading, quizzes, and certificates
- Cloud accounts, profile synchronization, leaderboards, and analytics
- Automatic installation of Rust or Go
- Academy-authored external-language project creation or execution

No deferred capability is exposed as complete.
