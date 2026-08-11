use std::fs;
use std::process::Command;
use std::time::{SystemTime, UNIX_EPOCH};

fn source_file(name: &str, source: &str) -> std::path::PathBuf {
    let unique = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let directory =
        std::env::temp_dir().join(format!("sprout-cli-{}-{unique}", std::process::id()));
    fs::create_dir_all(&directory).unwrap();
    let path = directory.join(name);
    fs::write(&path, source).unwrap();
    path
}

#[test]
fn run_executes_real_sprout_source() {
    let path = source_file(
        "hello.spr",
        "has name = \"Sprout\"\nrepeat 2 times\n  print(\"Hello \" + name)\nend\n",
    );
    let output = Command::new(env!("CARGO_BIN_EXE_sprout"))
        .args(["run", path.to_str().unwrap()])
        .output()
        .unwrap();
    assert!(output.status.success());
    assert_eq!(
        String::from_utf8(output.stdout).unwrap(),
        "Hello Sprout\nHello Sprout\n"
    );
    fs::remove_dir_all(path.parent().unwrap()).unwrap();
}

#[test]
fn invalid_source_returns_diagnostic_and_failure() {
    let path = source_file("broken.spr", "has answer =\n");
    let output = Command::new(env!("CARGO_BIN_EXE_sprout"))
        .args(["check", path.to_str().unwrap()])
        .output()
        .unwrap();
    assert_eq!(output.status.code(), Some(1));
    let stderr = String::from_utf8(output.stderr).unwrap();
    assert!(stderr.contains("error[SPT101]"), "{stderr}");
    assert!(stderr.contains("broken.spr:1:13"), "{stderr}");
    fs::remove_dir_all(path.parent().unwrap()).unwrap();
}

#[test]
fn semantic_member_error_renders_exact_symbol_and_suggestion() {
    let path = source_file(
        "member.spr",
        "class Player\n  action attack(enemy)\n    print(enemy)\n  end\nend\nhas player = Player()\nhas enemy = nil\nplayer.attck(enemy)\n",
    );
    let output = Command::new(env!("CARGO_BIN_EXE_sprout"))
        .args(["check", path.to_str().unwrap()])
        .output()
        .unwrap();
    assert_eq!(output.status.code(), Some(1));
    let stderr = String::from_utf8(output.stderr).unwrap();
    assert!(stderr.contains("error[SPT204]: Unknown member"), "{stderr}");
    assert!(stderr.contains("member.spr:8:8"), "{stderr}");
    assert!(
        stderr.contains("^^^^^ `Player` has no action named `attck`."),
        "{stderr}"
    );
    assert!(stderr.contains("Did you mean `attack`?"), "{stderr}");
    fs::remove_dir_all(path.parent().unwrap()).unwrap();
}

#[test]
fn all_syntax_profiles_execute_from_cli() {
    let cases = [
        ("guided", "has value = 3\nprint(value)\n"),
        ("structured", "int value = 3;\nprint(value);\n"),
        ("advanced", "value := 3\nprint(value)\n"),
    ];
    for (profile, source) in cases {
        let path = source_file(&format!("{profile}.spr"), source);
        let output = Command::new(env!("CARGO_BIN_EXE_sprout"))
            .args(["run", "--profile", profile, path.to_str().unwrap()])
            .output()
            .unwrap();
        assert!(
            output.status.success(),
            "{}",
            String::from_utf8_lossy(&output.stderr)
        );
        assert_eq!(String::from_utf8(output.stdout).unwrap(), "3\n");
        fs::remove_dir_all(path.parent().unwrap()).unwrap();
    }
}
