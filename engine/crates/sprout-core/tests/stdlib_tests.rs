use sprout_core::source::SourceFile;
use sprout_core::{SyntaxProfile, execute, execute_profile};
use std::fs;

#[test]
fn stdlib_math_functions_execute_cleanly() {
    let source = SourceFile::new(
        "test_math.spr",
        r#"
import math

print(math.abs(-42))
print(math.abs(15))
print(math.min(10, 20))
print(math.max(10, 20))
print(math.clamp(5, 10, 20))
print(math.clamp(25, 10, 20))
print(math.clamp(15, 10, 20))
print(math.sign(-99))
print(math.sign(99))
print(math.sign(0))
print(math.pow(2, 3))
print(math.pow(5, 0))
print(math.is_even(4))
print(math.is_even(7))
print(math.is_odd(9))
print(math.floor(3.7))
print(math.ceil(3.2))
print(math.round(3.6))
print(math.round(3.2))
print(math.lerp(0.0, 100.0, 0.25))
"#,
    );

    let result = execute(&source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(
        result.output,
        vec![
            "42", "15", "10", "20", "10", "20", "15", "-1", "1", "0", "8", "1", "true", "false",
            "true", "3", "4", "4", "3", "25",
        ]
    );
}

#[test]
fn stdlib_math_sqrt_and_constants() {
    let source = SourceFile::new(
        "test_sqrt.spr",
        r#"
import math

has root = math.sqrt(16)
print(root > 3.99 and root < 4.01)
print(math.PI > 3.14 and math.PI < 3.15)
print(math.E > 2.71 and math.E < 2.72)
"#,
    );

    let result = execute(&source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(result.output, vec!["true", "true", "true"]);
}

#[test]
fn stdlib_string_functions_execute_cleanly() {
    let source = SourceFile::new(
        "test_string.spr",
        r#"
import string

has text = "  Hello, Sprout!  "
print(string.len(text))
print(string.trim(text))
print(string.to_upper("sprout"))
print(string.to_lower("SPROUT"))
print(string.starts_with("abcdef", "abc"))
print(string.starts_with("abcdef", "xyz"))
print(string.ends_with("abcdef", "def"))
print(string.ends_with("abcdef", "xyz"))
print(string.contains("hello world", "world"))
print(string.contains("hello world", "rust"))
print(string.index_of("hello world", "world"))
print(string.index_of("hello world", "rust"))
print(string.slice("sprout", 1, 4))
print(string.replace("foo bar foo", "foo", "baz"))
print(string.repeat_str("ab", 3))
print(string.pad_start("42", 5, "0"))
print(string.pad_end("42", 5, "0"))
print(string.is_empty(""))
print(string.is_empty("not empty"))
"#,
    );

    let result = execute(&source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(
        result.output,
        vec![
            "18",
            "Hello, Sprout!",
            "SPROUT",
            "sprout",
            "true",
            "false",
            "true",
            "false",
            "true",
            "false",
            "6",
            "-1",
            "pro",
            "baz bar baz",
            "ababab",
            "00042",
            "42000",
            "true",
            "false",
        ]
    );
}

#[test]
fn stdlib_io_operations_execute_cleanly() {
    let temp_dir = std::env::temp_dir().join(format!("sprout_test_io_{}", std::process::id()));
    fs::create_dir_all(&temp_dir).unwrap();
    let file_path = temp_dir.join("test_file.txt");
    let file_path_str = file_path.to_string_lossy().replace('\\', "/");

    let source = SourceFile::new(
        "test_io.spr",
        format!(
            r#"
import io

has path = "{file_path_str}"
print(io.file_exists(path))
has ok_write = io.write_file(path, "Line 1\n")
print(ok_write)
print(io.file_exists(path))
print(io.read_file(path))
has ok_append = io.append_file(path, "Line 2\n")
print(ok_append)
print(io.read_file(path))
"#
        ),
    );

    let result = execute(&source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(
        result.output,
        vec![
            "false",
            "true",
            "true",
            "Line 1\n",
            "true",
            "Line 1\nLine 2\n",
        ]
    );

    let _ = fs::remove_dir_all(&temp_dir);
}

#[test]
fn stdlib_json_operations_execute_cleanly() {
    let source = SourceFile::new(
        "test_json.spr",
        r#"
import json

has valid = json.is_valid("{\"a\": 1}")
print(valid)
has invalid = json.is_valid("{\"a\": ")
print(invalid)

has parsed = json.parse("{\"title\": \"Sprout\", \"count\": 10, \"tags\": [\"lang\", \"std\"]}")
print(parsed["title"])
print(parsed["count"] + 5)
print(parsed["tags"][0])
print(parsed["tags"][1])

has out = json.stringify(parsed)
print(json.is_valid(out))
"#,
    );

    let result = execute(&source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(
        result.output,
        vec!["true", "false", "Sprout", "15", "lang", "std", "true"]
    );
}

#[test]
fn imports_work_across_all_syntax_profiles() {
    let guided_source = SourceFile::new(
        "guided.spr",
        r#"
import math as m
print(m.abs(-100))
"#,
    );
    let guided_res = execute_profile(&guided_source, SyntaxProfile::Guided);
    assert!(
        guided_res.diagnostics.is_empty(),
        "{:?}",
        guided_res.diagnostics
    );
    assert_eq!(guided_res.output, vec!["100"]);

    let structured_source = SourceFile::new(
        "structured.spr",
        r#"
import math as m;
print(m.abs(-200));
"#,
    );
    let structured_res = execute_profile(&structured_source, SyntaxProfile::Structured);
    assert!(
        structured_res.diagnostics.is_empty(),
        "{:?}",
        structured_res.diagnostics
    );
    assert_eq!(structured_res.output, vec!["200"]);

    let advanced_source = SourceFile::new(
        "advanced.spr",
        r#"
import math as m
print(m.abs(-300))
"#,
    );
    let advanced_res = execute_profile(&advanced_source, SyntaxProfile::Advanced);
    assert!(
        advanced_res.diagnostics.is_empty(),
        "{:?}",
        advanced_res.diagnostics
    );
    assert_eq!(advanced_res.output, vec!["300"]);
}

#[test]
fn local_project_module_resolution_and_shadowing() {
    let temp_dir = std::env::temp_dir().join(format!("sprout_mod_test_{}", std::process::id()));
    fs::create_dir_all(&temp_dir).unwrap();

    // Create a local helper module
    let helper_path = temp_dir.join("helper.spr");
    fs::write(
        &helper_path,
        r#"
action double_val(x)
    return x * 2
end
"#,
    )
    .unwrap();

    // Create a local math module shadowing stdlib math
    let local_math_path = temp_dir.join("math.spr");
    fs::write(
        &local_math_path,
        r#"
has PI = 999

action abs(x)
    return 777
end
"#,
    )
    .unwrap();

    let main_path = temp_dir.join("main.spr");
    let main_source = SourceFile::new(
        main_path.to_string_lossy().to_string(),
        r#"
import helper
import math

print(helper.double_val(21))
print(math.PI)
print(math.abs(-10))
"#,
    );

    let result = execute(&main_source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(result.output, vec!["42", "999", "777"]);

    let _ = fs::remove_dir_all(&temp_dir);
}

#[test]
fn missing_module_reports_clear_diagnostic() {
    let source = SourceFile::new(
        "missing_test.spr",
        r#"
import nonexistent_module_xyz
print("unreachable")
"#,
    );

    let result = execute(&source);
    assert!(!result.diagnostics.is_empty());
    assert_eq!(result.diagnostics[0].code, "SPT201");
}
