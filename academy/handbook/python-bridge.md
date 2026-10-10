# Borrowing Python Libraries

Python has libraries for almost everything. Sprout can call them directly, so you get Python's ecosystem with Sprout's friendly syntax.

## Importing a Python module

Use `import python`, the module name in quotes, and a short name to call it by:

```sprout
import python "math" as pymath
print(pymath.sqrt(81))
```

The import is always explicit, so it's clear which code comes from Python.

## Data comes back as Sprout values

Simple Python values (numbers, text, true/false, `None`) turn into their Sprout equivalents. Small lists and dictionaries convert too, so you can index them with square brackets:

```sprout
import python "json" as json
has data = json.loads("{\"items\": [10, 20]}")
print(data["items"][1])
```

Bigger or more complex Python objects stay as Python objects behind the scenes. You can still call their methods and read their members.

## Adding packages to a project

Projects list Python packages in the `[python]` section of `sprout.toml`. Use the **Packages** view to add one, and Studio installs it only into that project's private `.sprout/python` folder, never into your system Python.

## Good to know

- Studio bundles its own Python 3.13 for Windows x64.
- A failed import shows a clear error in **Problems** instead of crashing.
- Not every PyPI package is guaranteed to work; packages with native code must support Windows x64.

## Try it

Import Python's `random` module and print a random number between 1 and 6, like a die roll.
