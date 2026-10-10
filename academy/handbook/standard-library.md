# Batteries Included: The Standard Library

You don't have to write everything yourself. Sprout ships with ready-made modules for math, text, files, and data.

## Importing a module

Add an `import` line at the top, then use the module name with a dot:

```sprout
import math

print(math.max(10, 20))
print(math.pow(2, 8))
print(math.sqrt(25))
```

## Working with text

The `string` module cleans up, inspects, and changes text:

```sprout
import string

has raw = "  hello garden  "
has clean = string.trim(raw)
print(string.to_upper(clean))
print(string.contains(clean, "garden"))
print(string.len(clean))
```

## Reading data with json

JSON is a common text format for structured data. `json.parse` turns it into values you can look inside with square brackets:

```sprout
import json

has plant = json.parse("{\"name\": \"Fern\", \"height\": 30}")
print(plant["name"])
print(plant["height"])
```

## The modules at a glance

| Module | Handy for | Examples |
| :--- | :--- | :--- |
| `math` | numbers | `abs`, `min`, `max`, `pow`, `sqrt`, `round`, `clamp`, `PI` |
| `string` | text | `trim`, `to_upper`, `contains`, `replace`, `slice`, `len` |
| `io` | files | `read_file`, `write_file`, `file_exists` |
| `json` | data | `parse`, `stringify`, `is_valid` |

Switch this guide to **Full reference** to see every function with its exact behavior.

## Try it

Use `math.round` and `math.abs` to print the rounded distance between `-3.6` and `0`.
