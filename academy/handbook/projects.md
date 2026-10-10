# Projects in Plain English

Small experiments fit in one file. Real programs become **projects**: a folder that keeps your code, settings, and packages together.

## What's in a project

```text
MyGarden/
  sprout.toml      <- the project's settings
  src/
    main.spr       <- where your program starts
```

## The settings file: sprout.toml

```toml
manifest_version = 1

[project]
name = "MyGarden"
version = "0.1.0"
entry = "src/main.spr"

[sprout]
syntax = "guided"
```

- **name**: your project's name (letters, numbers, `-`, `_`).
- **version**: your own version number, like `1.2.0`.
- **entry**: the file that runs first.
- **syntax**: the profile for every Sprout file in the project: `guided`, `structured`, or `advanced`.

## Making one

Select **New Project** on the start page, choose a name and a template, and pick a folder. Studio creates the files and opens them for you.

## Running and building

- **Run** (`F5`) runs the project's entry file.
- **Check** (`Ctrl+F5`) looks for problems without running.
- **Build** (`Ctrl+Shift+B`) packages the project into a checked `.sproutbundle`.

## Packages

The `[python]` section lists Python packages the project needs. Manage them in the **Packages** view; each project keeps its own private copies, so projects never interfere with each other.

## Try it

Create a project called `HelloGarden`, change `main.spr` to print your name, and run it.
