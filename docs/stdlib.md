# Sprout Standard Library Reference

The Sprout Standard Library provides essential built-in modules for mathematics, string manipulation, file I/O, and JSON data serialization. Standard library modules are bundled directly inside native binaries and can be imported in any Sprout syntax profile (`guided`, `structured`, or `advanced`).

---

## 1. Math Module (`import math;`)

Mathematical constants and arithmetic operations.

### Constants
- **`math.PI`** (`Number`): `3.141592653589793` — Ratio of circle circumference to diameter.
- **`math.E`** (`Number`): `2.718281828459045` — Euler's constant, base of natural logarithms.
- **`math.TAU`** (`Number`): `6.283185307179586` — Circle constant (2π).

### Functions
| Signature | Returns | Description | Example |
| :--- | :--- | :--- | :--- |
| `math.abs(x)` | `Number` | Returns absolute magnitude | `math.abs(-42) # -> 42` |
| `math.min(a, b)` | `Number` | Minimum of two numbers | `math.min(10, 20) # -> 10` |
| `math.max(a, b)` | `Number` | Maximum of two numbers | `math.max(10, 20) # -> 20` |
| `math.clamp(val, min_val, max_val)` | `Number` | Clamps value within range | `math.clamp(150, 0, 100) # -> 100` |
| `math.sign(x)` | `Number` | Sign: 1 (pos), -1 (neg), 0 | `math.sign(-5) # -> -1` |
| `math.pow(base, exp)` | `Number` | Power computation | `math.pow(2, 8) # -> 256` |
| `math.sqrt(x)` | `Number` | Square root (Newton-Raphson) | `math.sqrt(25) # -> 5.0` |
| `math.floor(x)` | `Number` | Largest integer <= x | `math.floor(3.7) # -> 3` |
| `math.ceil(x)` | `Number` | Smallest integer >= x | `math.ceil(3.2) # -> 4` |
| `math.round(x)` | `Number` | Nearest integer | `math.round(4.6) # -> 5` |
| `math.is_even(n)` | `Boolean` | True if divisible by 2 | `math.is_even(4) # -> true` |
| `math.is_odd(n)` | `Boolean` | True if odd | `math.is_odd(5) # -> true` |
| `math.lerp(a, b, t)` | `Number` | Linear interpolation | `math.lerp(0, 100, 0.5) # -> 50.0` |
| `math.deg_to_rad(deg)` | `Number` | Degrees to radians | `math.deg_to_rad(180) # -> 3.14159` |
| `math.rad_to_deg(rad)` | `Number` | Radians to degrees | `math.rad_to_deg(3.14159) # -> 180` |
| `math.hypot(a, b)` | `Number` | Euclidean hypotenuse | `math.hypot(3, 4) # -> 5.0` |

---

## 2. String Module (`import string;`)

String inspection, formatting, slicing, and case conversions.

### Functions
| Signature | Returns | Description | Example |
| :--- | :--- | :--- | :--- |
| `string.len(text)` | `Number` | Character count | `string.len("hello") # -> 5` |
| `string.is_empty(text)` | `Boolean` | True if length is 0 | `string.is_empty("") # -> true` |
| `string.char_at(text, index)` | `String` | Character at zero-based index | `string.char_at("Sprout", 0) # -> "S"` |
| `string.slice(text, start, end)` | `String` | Substring slice | `string.slice("Sprout", 0, 3) # -> "Spr"` |
| `string.to_upper(text)` | `String` | ASCII uppercase | `string.to_upper("hello") # -> "HELLO"` |
| `string.to_lower(text)` | `String` | ASCII lowercase | `string.to_lower("WORLD") # -> "world"` |
| `string.starts_with(text, prefix)` | `Boolean` | Prefix match | `string.starts_with("main.spr", "main") # -> true` |
| `string.ends_with(text, suffix)` | `Boolean` | Suffix match | `string.ends_with("main.spr", ".spr") # -> true` |
| `string.contains(text, search)` | `Boolean` | Substring presence | `string.contains("apple pie", "pie") # -> true` |
| `string.index_of(text, search)` | `Number` | Index or -1 | `string.index_of("banana", "na") # -> 2` |
| `string.trim(text)` | `String` | Trim whitespace | `string.trim("  hi  ") # -> "hi"` |
| `string.trim_start(text)` | `String` | Trim leading whitespace | `string.trim_start("  hi") # -> "hi"` |
| `string.trim_end(text)` | `String` | Trim trailing whitespace | `string.trim_end("hi  ") # -> "hi"` |
| `string.repeat_str(text, count)` | `String` | Duplicate string | `string.repeat_str("ab", 3) # -> "ababab"` |
| `string.replace(text, old, new)` | `String` | Replace substring | `string.replace("cat and cat", "cat", "dog")` |
| `string.pad_start(text, len, pad)` | `String` | Left pad | `string.pad_start("42", 5, "0") # -> "00042"` |
| `string.pad_end(text, len, pad)` | `String` | Right pad | `string.pad_end("42", 5, "0") # -> "42000"` |

---

## 3. IO Module (`import io;`)

File system operations and console streaming.

### Functions
| Signature | Returns | Description | Example |
| :--- | :--- | :--- | :--- |
| `io.read_file(path)` | `String` | Reads UTF-8 file contents | `io.read_file("data.txt")` |
| `io.write_file(path, content)` | `Boolean` | Overwrites file with content | `io.write_file("out.txt", "data")` |
| `io.append_file(path, content)` | `Boolean` | Appends text to file | `io.append_file("log.txt", "line\n")` |
| `io.file_exists(path)` | `Boolean` | Tests if path exists | `io.file_exists("config.json") # -> true` |
| `io.print_line(message)` | `Nil` | Prints message with newline | `io.print_line("Ready")` |

---

## 4. JSON Module (`import json;`)

Encoding, decoding, and validation of JSON data.

### Functions
| Signature | Returns | Description | Example |
| :--- | :--- | :--- | :--- |
| `json.parse(json_text)` | `Any` | Parses JSON string to Sprout value | `json.parse("{\"id\": 1}")` |
| `json.stringify(value)` | `String` | Encodes Sprout value to JSON | `json.stringify({"ok": true})` |
| `json.is_valid(json_text)` | `Boolean` | Non-throwing JSON validator | `json.is_valid("{\"ok\": true}") # -> true` |
