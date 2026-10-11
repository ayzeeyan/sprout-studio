//! Embedded Sprout Standard Library assets, resolution helpers, and structured documentation metadata.

pub const MATH_SOURCE: &str = include_str!("../../../../stdlib/math.spr");
pub const STRING_SOURCE: &str = include_str!("../../../../stdlib/string.spr");
pub const IO_SOURCE: &str = include_str!("../../../../stdlib/io.spr");
pub const JSON_SOURCE: &str = include_str!("../../../../stdlib/json.spr");

pub const STDLIB_MODULES: &[&str] = &["math", "string", "io", "json"];

/// Returns the embedded source code for a standard library module if known.
pub fn get_embedded_module(name: &str) -> Option<&'static str> {
    match name {
        "math" => Some(MATH_SOURCE),
        "string" => Some(STRING_SOURCE),
        "io" => Some(IO_SOURCE),
        "json" => Some(JSON_SOURCE),
        _ => None,
    }
}

/// Checks if a module name corresponds to an embedded standard library module.
pub fn is_stdlib_module(name: &str) -> bool {
    matches!(name, "math" | "string" | "io" | "json")
}

/// Structured documentation metadata for a standard library symbol.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct StdlibSymbolDoc {
    pub module: &'static str,
    pub name: &'static str,
    pub kind: &'static str,
    pub signature: &'static str,
    pub description: &'static str,
    pub params: &'static [(&'static str, &'static str, &'static str)],
    pub returns: Option<(&'static str, &'static str)>,
    pub example: &'static str,
}

pub static STDLIB_DOCS: &[StdlibSymbolDoc] = &[
    // --- MATH MODULE ---
    StdlibSymbolDoc {
        module: "math",
        name: "PI",
        kind: "constant",
        signature: "math.PI : Number",
        description: "Mathematical constant representing the ratio of a circle's circumference to its diameter (π ≈ 3.14159).",
        params: &[],
        returns: Some(("Number", "3.141592653589793")),
        example: "math.PI",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "E",
        kind: "constant",
        signature: "math.E : Number",
        description: "Euler's number, the base of natural logarithms (e ≈ 2.71828).",
        params: &[],
        returns: Some(("Number", "2.718281828459045")),
        example: "math.E",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "TAU",
        kind: "constant",
        signature: "math.TAU : Number",
        description: "Mathematical constant representing the circle constant TAU (τ = 2π ≈ 6.28318).",
        params: &[],
        returns: Some(("Number", "6.283185307179586")),
        example: "math.TAU",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "abs",
        kind: "action",
        signature: "math.abs(x: Number) -> Number",
        description: "Returns the absolute value and non-negative magnitude of a number.",
        params: &[("x", "Number", "The input number.")],
        returns: Some(("Number", "Non-negative magnitude of x.")),
        example: "math.abs(-42) # -> 42",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "min",
        kind: "action",
        signature: "math.min(a: Number, b: Number) -> Number",
        description: "Returns the smaller of two numbers.",
        params: &[
            ("a", "Number", "First number."),
            ("b", "Number", "Second number."),
        ],
        returns: Some(("Number", "The minimum value.")),
        example: "math.min(10, 20) # -> 10",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "max",
        kind: "action",
        signature: "math.max(a: Number, b: Number) -> Number",
        description: "Returns the larger of two numbers.",
        params: &[
            ("a", "Number", "First number."),
            ("b", "Number", "Second number."),
        ],
        returns: Some(("Number", "The maximum value.")),
        example: "math.max(10, 20) # -> 20",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "clamp",
        kind: "action",
        signature: "math.clamp(val: Number, min_val: Number, max_val: Number) -> Number",
        description: "Restricts a value to lie within the inclusive range [min_val, max_val].",
        params: &[
            ("val", "Number", "The value to clamp."),
            ("min_val", "Number", "Lower bound limit."),
            ("max_val", "Number", "Upper bound limit."),
        ],
        returns: Some(("Number", "The clamped value within range.")),
        example: "math.clamp(150, 0, 100) # -> 100",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "sign",
        kind: "action",
        signature: "math.sign(x: Number) -> Number",
        description: "Returns the sign of a number (1 for positive, -1 for negative, 0 for zero).",
        params: &[("x", "Number", "Input number.")],
        returns: Some(("Number", "1, -1, or 0.")),
        example: "math.sign(-5) # -> -1",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "pow",
        kind: "action",
        signature: "math.pow(base: Number, exp: Number) -> Number",
        description: "Computes base raised to the power of integer exp.",
        params: &[
            ("base", "Number", "Base value."),
            ("exp", "Number", "Integer exponent."),
        ],
        returns: Some(("Number", "base raised to exp.")),
        example: "math.pow(2, 8) # -> 256",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "sqrt",
        kind: "action",
        signature: "math.sqrt(x: Number) -> Number",
        description: "Computes the square root of a non-negative number using Newton-Raphson approximation.",
        params: &[("x", "Number", "Non-negative number to evaluate.")],
        returns: Some(("Number", "Computed square root, or 0 if x <= 0.")),
        example: "math.sqrt(25) # -> 5.0",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "floor",
        kind: "action",
        signature: "math.floor(x: Number) -> Number",
        description: "Returns the largest integer less than or equal to x.",
        params: &[("x", "Number", "Input number.")],
        returns: Some(("Number", "Floored integer.")),
        example: "math.floor(3.7) # -> 3",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "ceil",
        kind: "action",
        signature: "math.ceil(x: Number) -> Number",
        description: "Returns the smallest integer greater than or equal to x.",
        params: &[("x", "Number", "Input number.")],
        returns: Some(("Number", "Ceiled integer.")),
        example: "math.ceil(3.2) # -> 4",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "round",
        kind: "action",
        signature: "math.round(x: Number) -> Number",
        description: "Rounds a number to the nearest integer.",
        params: &[("x", "Number", "Input number.")],
        returns: Some(("Number", "Nearest rounded integer.")),
        example: "math.round(4.6) # -> 5",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "is_even",
        kind: "action",
        signature: "math.is_even(n: Number) -> Boolean",
        description: "Checks if an integer n is divisible by 2.",
        params: &[("n", "Number", "Integer to test.")],
        returns: Some(("Boolean", "True if even, false otherwise.")),
        example: "math.is_even(4) # -> true",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "is_odd",
        kind: "action",
        signature: "math.is_odd(n: Number) -> Boolean",
        description: "Checks if an integer n is odd.",
        params: &[("n", "Number", "Integer to test.")],
        returns: Some(("Boolean", "True if odd, false otherwise.")),
        example: "math.is_odd(5) # -> true",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "lerp",
        kind: "action",
        signature: "math.lerp(a: Number, b: Number, t: Number) -> Number",
        description: "Linearly interpolates between a and b by fraction t.",
        params: &[
            ("a", "Number", "Start value."),
            ("b", "Number", "End value."),
            ("t", "Number", "Interpolation factor (0.0 to 1.0)."),
        ],
        returns: Some(("Number", "Interpolated result.")),
        example: "math.lerp(0, 100, 0.5) # -> 50.0",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "deg_to_rad",
        kind: "action",
        signature: "math.deg_to_rad(deg: Number) -> Number",
        description: "Converts angle in degrees to radians.",
        params: &[("deg", "Number", "Angle in degrees.")],
        returns: Some(("Number", "Angle in radians.")),
        example: "math.deg_to_rad(180) # -> 3.14159",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "rad_to_deg",
        kind: "action",
        signature: "math.rad_to_deg(rad: Number) -> Number",
        description: "Converts angle in radians to degrees.",
        params: &[("rad", "Number", "Angle in radians.")],
        returns: Some(("Number", "Angle in degrees.")),
        example: "math.rad_to_deg(3.14159) # -> 180",
    },
    StdlibSymbolDoc {
        module: "math",
        name: "hypot",
        kind: "action",
        signature: "math.hypot(a: Number, b: Number) -> Number",
        description: "Calculates the Euclidean hypotenuse sqrt(a^2 + b^2).",
        params: &[
            ("a", "Number", "First leg length."),
            ("b", "Number", "Second leg length."),
        ],
        returns: Some(("Number", "Hypotenuse length.")),
        example: "math.hypot(3, 4) # -> 5.0",
    },
    // --- STRING MODULE ---
    StdlibSymbolDoc {
        module: "string",
        name: "len",
        kind: "action",
        signature: "string.len(text: String) -> Number",
        description: "Returns the number of characters in a string.",
        params: &[("text", "String", "The input string.")],
        returns: Some(("Number", "Character count.")),
        example: "string.len(\"hello\") # -> 5",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "is_empty",
        kind: "action",
        signature: "string.is_empty(text: String) -> Boolean",
        description: "Checks if a string has a length of 0.",
        params: &[("text", "String", "The string to inspect.")],
        returns: Some(("Boolean", "True if empty, false otherwise.")),
        example: "string.is_empty(\"\") # -> true",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "char_at",
        kind: "action",
        signature: "string.char_at(text: String, index: Number) -> String",
        description: "Returns the single character at the specified zero-based index.",
        params: &[
            ("text", "String", "Source string."),
            ("index", "Number", "Zero-based character index."),
        ],
        returns: Some(("String", "Character substring.")),
        example: "string.char_at(\"Sprout\", 0) # -> \"S\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "slice",
        kind: "action",
        signature: "string.slice(text: String, start_idx: Number, end_idx: Number) -> String",
        description: "Extracts a substring from start_idx up to (exclusive) end_idx.",
        params: &[
            ("text", "String", "Source string."),
            ("start_idx", "Number", "Beginning index."),
            ("end_idx", "Number", "Ending index (exclusive)."),
        ],
        returns: Some(("String", "Extracted substring.")),
        example: "string.slice(\"Sprout\", 0, 3) # -> \"Spr\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "to_upper",
        kind: "action",
        signature: "string.to_upper(text: String) -> String",
        description: "Converts all ASCII lowercase characters in text to uppercase.",
        params: &[("text", "String", "Input string.")],
        returns: Some(("String", "Uppercase string.")),
        example: "string.to_upper(\"hello\") # -> \"HELLO\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "to_lower",
        kind: "action",
        signature: "string.to_lower(text: String) -> String",
        description: "Converts all ASCII uppercase characters in text to lowercase.",
        params: &[("text", "String", "Input string.")],
        returns: Some(("String", "Lowercase string.")),
        example: "string.to_lower(\"WORLD\") # -> \"world\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "starts_with",
        kind: "action",
        signature: "string.starts_with(text: String, prefix: String) -> Boolean",
        description: "Determines whether a string starts with the given prefix.",
        params: &[
            ("text", "String", "Input string."),
            ("prefix", "String", "Prefix substring."),
        ],
        returns: Some(("Boolean", "True if prefix matches.")),
        example: "string.starts_with(\"main.spr\", \"main\") # -> true",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "ends_with",
        kind: "action",
        signature: "string.ends_with(text: String, suffix: String) -> Boolean",
        description: "Determines whether a string ends with the given suffix.",
        params: &[
            ("text", "String", "Input string."),
            ("suffix", "String", "Suffix substring."),
        ],
        returns: Some(("Boolean", "True if suffix matches.")),
        example: "string.ends_with(\"main.spr\", \".spr\") # -> true",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "contains",
        kind: "action",
        signature: "string.contains(text: String, search: String) -> Boolean",
        description: "Returns true if text contains the search substring.",
        params: &[
            ("text", "String", "Source string."),
            ("search", "String", "Substring to locate."),
        ],
        returns: Some(("Boolean", "True if found.")),
        example: "string.contains(\"apple pie\", \"pie\") # -> true",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "index_of",
        kind: "action",
        signature: "string.index_of(text: String, search: String) -> Number",
        description: "Returns the zero-based index of the first occurrence of search, or -1 if not found.",
        params: &[
            ("text", "String", "Source string."),
            ("search", "String", "Target substring."),
        ],
        returns: Some(("Number", "Zero-based index or -1.")),
        example: "string.index_of(\"banana\", \"na\") # -> 2",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "trim",
        kind: "action",
        signature: "string.trim(text: String) -> String",
        description: "Removes all leading and trailing whitespace characters.",
        params: &[("text", "String", "Input string.")],
        returns: Some(("String", "Trimmed string.")),
        example: "string.trim(\"  sprout  \") # -> \"sprout\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "trim_start",
        kind: "action",
        signature: "string.trim_start(text: String) -> String",
        description: "Removes leading whitespace characters.",
        params: &[("text", "String", "Input string.")],
        returns: Some(("String", "Start-trimmed string.")),
        example: "string.trim_start(\"  hello\") # -> \"hello\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "trim_end",
        kind: "action",
        signature: "string.trim_end(text: String) -> String",
        description: "Removes trailing whitespace characters.",
        params: &[("text", "String", "Input string.")],
        returns: Some(("String", "End-trimmed string.")),
        example: "string.trim_end(\"hello  \") # -> \"hello\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "repeat_str",
        kind: "action",
        signature: "string.repeat_str(text: String, count: Number) -> String",
        description: "Constructs a new string by repeating text count times.",
        params: &[
            ("text", "String", "String to repeat."),
            ("count", "Number", "Number of repetitions."),
        ],
        returns: Some(("String", "Repeated string.")),
        example: "string.repeat_str(\"ab\", 3) # -> \"ababab\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "replace",
        kind: "action",
        signature: "string.replace(text: String, old_str: String, new_str: String) -> String",
        description: "Replaces all occurrences of old_str with new_str.",
        params: &[
            ("text", "String", "Source string."),
            ("old_str", "String", "Target substring to replace."),
            ("new_str", "String", "Replacement string."),
        ],
        returns: Some(("String", "Modified string.")),
        example: "string.replace(\"cat and cat\", \"cat\", \"dog\") # -> \"dog and dog\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "pad_start",
        kind: "action",
        signature: "string.pad_start(text: String, target_len: Number, pad_char: String) -> String",
        description: "Pads the beginning of the string with pad_char until target_len is reached.",
        params: &[
            ("text", "String", "Input string."),
            ("target_len", "Number", "Desired output length."),
            ("pad_char", "String", "Padding character."),
        ],
        returns: Some(("String", "Left-padded string.")),
        example: "string.pad_start(\"42\", 5, \"0\") # -> \"00042\"",
    },
    StdlibSymbolDoc {
        module: "string",
        name: "pad_end",
        kind: "action",
        signature: "string.pad_end(text: String, target_len: Number, pad_char: String) -> String",
        description: "Pads the end of the string with pad_char until target_len is reached.",
        params: &[
            ("text", "String", "Input string."),
            ("target_len", "Number", "Desired output length."),
            ("pad_char", "String", "Padding character."),
        ],
        returns: Some(("String", "Right-padded string.")),
        example: "string.pad_end(\"42\", 5, \"0\") # -> \"42000\"",
    },
    // --- IO MODULE ---
    StdlibSymbolDoc {
        module: "io",
        name: "read_file",
        kind: "action",
        signature: "io.read_file(path: String) -> String",
        description: "Reads the entire contents of a file at path into a UTF-8 string.",
        params: &[("path", "String", "File path to read.")],
        returns: Some(("String", "File content text.")),
        example: "io.read_file(\"data.txt\")",
    },
    StdlibSymbolDoc {
        module: "io",
        name: "write_file",
        kind: "action",
        signature: "io.write_file(path: String, content: String) -> Boolean",
        description: "Writes content to a file at path, overwriting any existing file.",
        params: &[
            ("path", "String", "Destination file path."),
            ("content", "String", "Text content to write."),
        ],
        returns: Some(("Boolean", "True on success.")),
        example: "io.write_file(\"output.txt\", \"Hello World\")",
    },
    StdlibSymbolDoc {
        module: "io",
        name: "append_file",
        kind: "action",
        signature: "io.append_file(path: String, content: String) -> Boolean",
        description: "Appends text content to the end of a file at path.",
        params: &[
            ("path", "String", "Destination file path."),
            ("content", "String", "Text content to append."),
        ],
        returns: Some(("Boolean", "True on success.")),
        example: "io.append_file(\"log.txt\", \"New log entry\\n\")",
    },
    StdlibSymbolDoc {
        module: "io",
        name: "file_exists",
        kind: "action",
        signature: "io.file_exists(path: String) -> Boolean",
        description: "Checks whether a file or directory exists at the given path.",
        params: &[("path", "String", "File system path to inspect.")],
        returns: Some(("Boolean", "True if the path exists.")),
        example: "io.file_exists(\"config.json\")",
    },
    StdlibSymbolDoc {
        module: "io",
        name: "print_line",
        kind: "action",
        signature: "io.print_line(message: Any)",
        description: "Prints a message followed by a newline to standard output.",
        params: &[("message", "Any", "Value to print.")],
        returns: None,
        example: "io.print_line(\"Operation complete\")",
    },
    // --- JSON MODULE ---
    StdlibSymbolDoc {
        module: "json",
        name: "parse",
        kind: "action",
        signature: "json.parse(json_text: String) -> Any",
        description: "Parses a JSON string into native Sprout objects, lists, numbers, booleans, or strings.",
        params: &[("json_text", "String", "JSON encoded string.")],
        returns: Some(("Any", "Decoded Sprout data structure.")),
        example: "json.parse(\"{\\\"name\\\": \\\"Alice\\\"}\")",
    },
    StdlibSymbolDoc {
        module: "json",
        name: "stringify",
        kind: "action",
        signature: "json.stringify(value: Any) -> String",
        description: "Serializes a Sprout value or collection into a JSON formatted string.",
        params: &[("value", "Any", "Value to serialize.")],
        returns: Some(("String", "JSON formatted text.")),
        example: "json.stringify({\"status\": 200})",
    },
    StdlibSymbolDoc {
        module: "json",
        name: "is_valid",
        kind: "action",
        signature: "json.is_valid(json_text: String) -> Boolean",
        description: "Validates whether a string is well-formed JSON syntax without throwing an error.",
        params: &[("json_text", "String", "JSON text to test.")],
        returns: Some(("Boolean", "True if valid JSON, false otherwise.")),
        example: "json.is_valid(\"{\\\"valid\\\": true}\")",
    },
];

/// Returns structured documentation metadata for a standard library symbol.
pub fn get_stdlib_symbol_doc(module: &str, symbol: &str) -> Option<&'static StdlibSymbolDoc> {
    STDLIB_DOCS
        .iter()
        .find(|doc| doc.module == module && doc.name == symbol)
}

/// Returns all symbols documented under a specific standard library module.
pub fn get_module_symbols(module: &str) -> Vec<&'static StdlibSymbolDoc> {
    STDLIB_DOCS
        .iter()
        .filter(|doc| doc.module == module)
        .collect()
}

/// Serializes all standard library symbol documentation into a JSON string.
pub fn get_stdlib_docs_json() -> String {
    let mut json = String::from("[\n");
    for (i, doc) in STDLIB_DOCS.iter().enumerate() {
        if i > 0 {
            json.push_str(",\n");
        }
        json.push_str("  {\n");
        json.push_str(&format!("    \"module\": \"{}\",\n", doc.module));
        json.push_str(&format!("    \"name\": \"{}\",\n", doc.name));
        json.push_str(&format!("    \"kind\": \"{}\",\n", doc.kind));
        json.push_str(&format!(
            "    \"signature\": \"{}\",\n",
            doc.signature.replace('\"', "\\\"")
        ));
        json.push_str(&format!(
            "    \"description\": \"{}\",\n",
            doc.description.replace('\"', "\\\"")
        ));
        json.push_str("    \"params\": [\n");
        for (j, (pname, ptype, pdesc)) in doc.params.iter().enumerate() {
            if j > 0 {
                json.push_str(",\n");
            }
            json.push_str(&format!(
                "      {{\"name\": \"{}\", \"type\": \"{}\", \"description\": \"{}\"}}",
                pname,
                ptype,
                pdesc.replace('\"', "\\\"")
            ));
        }
        json.push_str("\n    ],\n");
        if let Some((rtype, rdesc)) = doc.returns {
            json.push_str(&format!(
                "    \"returns\": {{\"type\": \"{}\", \"description\": \"{}\"}},\n",
                rtype,
                rdesc.replace('\"', "\\\"")
            ));
        } else {
            json.push_str("    \"returns\": null,\n");
        }
        json.push_str(&format!(
            "    \"example\": \"{}\"\n",
            doc.example.replace('\"', "\\\"").replace('\n', "\\n")
        ));
        json.push_str("  }");
    }
    json.push_str("\n]");
    json
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn stdlib_modules_are_bundled_and_non_empty() {
        for module_name in STDLIB_MODULES {
            let source = get_embedded_module(module_name);
            assert!(source.is_some(), "Module {module_name} should be embedded");
            assert!(
                !source.unwrap().trim().is_empty(),
                "Module {module_name} should not be empty"
            );
        }
    }

    #[test]
    fn stdlib_docs_metadata_is_complete() {
        assert!(
            STDLIB_DOCS.len() >= 25,
            "Expected at least 25 documented stdlib symbols"
        );
        let sqrt_doc = get_stdlib_symbol_doc("math", "sqrt");
        assert!(sqrt_doc.is_some(), "math.sqrt doc should exist");
        let doc = sqrt_doc.unwrap();
        assert_eq!(doc.name, "sqrt");
        assert_eq!(doc.params.len(), 1);
        assert!(doc.description.contains("Newton-Raphson"));
    }

    #[test]
    fn stdlib_docs_json_serializes_cleanly() {
        let json = get_stdlib_docs_json();
        assert!(json.starts_with('['));
        assert!(json.ends_with(']'));
        assert!(json.contains("\"name\": \"sqrt\""));
        assert!(json.contains("\"module\": \"math\""));
    }
}
