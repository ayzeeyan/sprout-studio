# Sprout Projects

A Sprout project is a directory containing a versioned `sprout.toml` manifest and at least one `.spr` entry file.

```toml
manifest_version = 1

[project]
name = "AsteroidGame"
version = "0.1.0"
entry = "src/main.spr"

[sprout]
syntax = "advanced"

[dependencies]

[python]
pygame = "2.6.*"

[build]
target = "windows-x86_64"
```

`manifest_version` is required and currently accepts only `1`. Project names start with an ASCII letter and contain letters, numbers, `-`, or `_`. Versions use numeric `major.minor.patch` form. The entry must be a relative `.spr` path without parent traversal and must resolve inside the project root. Syntax is `guided`, `structured`, or `advanced`.

Dependency names and non-empty requirements are validated. Installation is deliberately deferred: `[python]` entries will use the project-local `.sprout/python` environment in Phase 6 and will never be installed into a shared global project directory.

## Create a project

```powershell
sprout new HelloSprout
sprout new StructuredApp --template structured
sprout new AdvancedApp --template advanced --path C:\Projects\AdvancedApp
```

Without `--path`, the exact destination is a new directory matching the project name under the current directory. Creation refuses to overwrite an existing destination.

## Check and run

From inside a project:

```powershell
sprout check
sprout run
```

Or pass the project directory or manifest explicitly:

```powershell
sprout check C:\Projects\HelloSprout
sprout run C:\Projects\HelloSprout\sprout.toml
```

The CLI loads the manifest entry and syntax profile through `sprout-project`, the same Rust API used by Sprout Studio.
