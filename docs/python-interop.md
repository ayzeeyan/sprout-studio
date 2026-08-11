# Python Interoperability

Sprout executes its own canonical AST in the Rust runtime. Python is an explicit foreign-language subsystem and is never used to parse, translate, `eval`, or `exec` Sprout source.

## Import and call Python

```text
import python "math" as math
print(math.sqrt(81))
```

The module string must contain a valid dotted Python module name. The alias is a normal lexically scoped Sprout symbol. Python imports that fail produce an `SPT301` diagnostic containing the Python exception type and message; they do not unwind through the Rust or C ABI boundaries.

## Values and proxies

Python `int`, `float`, `bool`, `str`, and `None` convert to their natural Sprout equivalents. Lists and dictionaries with string keys convert recursively when their depth and item count are within conservative limits. Large collections, dictionaries with non-string keys, modules, functions, classes, and application objects remain opaque proxies.

Proxies forward member access, method/function calls, and indexing to CPython. Arguments and return values pass through the same checked conversion layer:

```text
import python "json" as json
has data = json.loads("{\"items\": [10, 20]}")
print(data["items"][1])
```

This design avoids copying large objects such as arrays, surfaces, or tensors merely to cross the language boundary. Compatibility is not promised for every PyPI package: native wheels must support the pinned CPython version, Windows x86-64, and the package's own runtime assumptions.

## Embedded runtime

Windows distributions use the official CPython 3.13.14 x64 embeddable runtime through PyO3's stable Python 3.13 ABI. pip 26.2.1 is carried as a pinned wheel. The preparation script verifies SHA-256 before either artifact becomes part of a build, and Python's `site` initialization remains disabled so global and user site-packages are not silently added.

Studio and packaged CLI directories place `python3.dll`, `python313.dll`, the standard-library archive, extension modules, and pip beside the Sprout executable. A supported end user therefore does not install Python separately.

## Project packages

Declare packages in `sprout.toml`:

```toml
[python]
colorama = "0.4.6"
numpy = ">=2.0"
```

Then synchronize them:

```powershell
sprout python sync
```

Sprout invokes bundled Python as a child process using individual process arguments and installs with pip's `--target` into `<project>/.sprout/python`. The directory is ignored by generated projects. Each project run prepends only its own package directory and removes modules loaded from that directory before another project can run.

Package execution is not a sandbox. Sprout source and imported Python packages can perform the operations allowed to the current Windows user.
