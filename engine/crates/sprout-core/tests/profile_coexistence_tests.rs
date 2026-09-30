use sprout_core::formatter::format_profile;
use sprout_core::ide::inspect;
use sprout_core::parser::{SyntaxProfile, parse_profile};
use sprout_core::source::SourceFile;
use sprout_core::{execute, execute_profile};

#[test]
fn mixed_profiles_share_state_and_execute_cleanly() {
    let source_text = r#"
guided {
    has base = 100
    has multiplier = 2
}

structured {
    int added = base + 50;
    int scaled = added * multiplier;
    print(scaled);
}

advanced {
    final_val := scaled + 25
    print(final_val)
}
"#;
    let source = SourceFile::new("mixed.spr", source_text);
    let parsed = parse_profile(&source, SyntaxProfile::Guided);
    assert!(
        parsed.diagnostics.is_empty(),
        "Parsing failed: {:?}",
        parsed.diagnostics
    );
    let program = parsed.program.expect("program parsed successfully");
    assert_eq!(program.statements.len(), 3);

    let result = execute(&source);
    assert!(
        result.diagnostics.is_empty(),
        "Execution failed: {:?}",
        result.diagnostics
    );

    assert_eq!(
        result.output,
        vec![String::from("300"), String::from("325")]
    );
}

#[test]
fn profile_block_functions_and_classes_callable_across_blocks() {
    let source_text = r#"
guided {
    action calculate_tax(amount, rate)
        return amount * rate
    end
}

structured {
    auto tax = calculate_tax(1000, 15);
    print(tax);
}

advanced {
    class Wallet =>
        balance() =>
            return 500
}

guided {
    has my_wallet = Wallet()
    has b = my_wallet.balance()
    print(b)
}
"#;
    let source = SourceFile::new("interop.spr", source_text);
    let result = execute(&source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(
        result.output,
        vec![String::from("15000"), String::from("500")]
    );
}

#[test]
fn nested_profile_blocks_execute_cleanly() {
    let source_text = r#"
guided {
    has outer = 10
    structured {
        int inner = outer + 5;
        advanced {
            deep := inner * 2
            print(deep)
        }
    }
}
"#;
    let source = SourceFile::new("nested.spr", source_text);
    let result = execute(&source);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(result.output, vec![String::from("30")]);
}

#[test]
fn formatter_roundtrips_profile_blocks() {
    let source_text = r#"guided {
    has a = 10
}
structured {
    int b = 20;
}
advanced {
    c := 30
}
"#;
    let source = SourceFile::new("roundtrip.spr", source_text);
    let format_result = format_profile(&source, SyntaxProfile::Guided);
    assert!(
        format_result.diagnostics.is_empty(),
        "{:?}",
        format_result.diagnostics
    );
    let formatted = format_result.formatted_source.expect("formatted source");

    let source_reparsed = SourceFile::new("reparsed.spr", formatted.as_str());
    let reparsed = parse_profile(&source_reparsed, SyntaxProfile::Guided);
    assert!(
        reparsed.diagnostics.is_empty(),
        "Reparse diagnostics: {:?}",
        reparsed.diagnostics
    );

    let re_exec = execute(&source_reparsed);
    assert!(re_exec.diagnostics.is_empty(), "{:?}", re_exec.diagnostics);
}

#[test]
fn ide_inspection_resolves_profile_keywords_at_cursor() {
    let source_text = r#"guided {
    has val = 1
}
structured {
    int x = 2;
}
advanced {
    y := 3
}
"#;
    let source = SourceFile::new("ide_test.spr", source_text);

    // Cursor inside `guided {` block
    let guided_cursor = source_text.find("has val").unwrap();
    let result_guided = inspect(&source, SyntaxProfile::Guided, guided_cursor);
    assert!(
        result_guided
            .completions
            .iter()
            .any(|c| c.label == "action" && c.kind == "keyword"),
        "Guided block should contain 'action' keyword completion"
    );

    // Cursor inside `structured {` block
    let structured_cursor = source_text.find("int x").unwrap();
    let result_structured = inspect(&source, SyntaxProfile::Guided, structured_cursor);
    assert!(
        result_structured
            .completions
            .iter()
            .any(|c| c.label == "auto" && c.kind == "keyword"),
        "Structured block should contain 'auto' keyword completion"
    );

    // Symbol outline captures variables across blocks
    assert!(
        result_guided
            .completions
            .iter()
            .any(|c| c.label == "val" && c.kind == "variable")
    );
}

#[test]
fn stdlib_imports_work_seamlessly_inside_mixed_profile_blocks() {
    let source_text = r#"
guided {
    import math
    has root = math.sqrt(64)
}

structured {
    import string;
    auto msg = string.to_upper("sprout");
    print(msg);
}

advanced {
    import json
    parsed := json.parse("{\"count\": 42}")
    print(parsed["count"])
    print(root)
}
"#;
    let source = SourceFile::new("stdlib_mixed.spr", source_text);
    let result = execute_profile(&source, SyntaxProfile::Guided);
    assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    assert_eq!(
        result.output,
        vec![
            String::from("SPROUT"),
            String::from("42"),
            String::from("8")
        ]
    );
}
