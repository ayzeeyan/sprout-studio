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

fn temporary_directory(name: &str) -> std::path::PathBuf {
    let unique = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    std::env::temp_dir().join(format!("sprout-cli-{}-{name}-{unique}", std::process::id()))
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

#[test]
fn new_creates_and_runs_each_project_template() {
    for template in ["guided", "structured", "advanced"] {
        let destination = temporary_directory(template).join("HelloProject");
        let create = Command::new(env!("CARGO_BIN_EXE_sprout"))
            .args([
                "new",
                "HelloProject",
                "--template",
                template,
                "--path",
                destination.to_str().unwrap(),
            ])
            .output()
            .unwrap();
        assert!(
            create.status.success(),
            "{}",
            String::from_utf8_lossy(&create.stderr)
        );
        assert!(destination.join("sprout.toml").is_file());
        assert!(destination.join("src/main.spr").is_file());

        let run = Command::new(env!("CARGO_BIN_EXE_sprout"))
            .args(["run", destination.to_str().unwrap()])
            .output()
            .unwrap();
        assert!(
            run.status.success(),
            "{}",
            String::from_utf8_lossy(&run.stderr)
        );
        assert_eq!(
            String::from_utf8(run.stdout).unwrap(),
            "Welcome to HelloProject\n"
        );
        fs::remove_dir_all(destination.parent().unwrap()).unwrap();
    }
}

#[test]
fn run_uses_the_manifest_syntax_profile() {
    let destination = temporary_directory("profile").join("StructuredProject");
    let create = Command::new(env!("CARGO_BIN_EXE_sprout"))
        .args([
            "new",
            "StructuredProject",
            "--template",
            "structured",
            "--path",
            destination.to_str().unwrap(),
        ])
        .output()
        .unwrap();
    assert!(create.status.success());
    let run = Command::new(env!("CARGO_BIN_EXE_sprout"))
        .current_dir(&destination)
        .arg("run")
        .output()
        .unwrap();
    assert!(
        run.status.success(),
        "{}",
        String::from_utf8_lossy(&run.stderr)
    );
    assert_eq!(
        String::from_utf8(run.stdout).unwrap(),
        "Welcome to StructuredProject\n"
    );
    fs::remove_dir_all(destination.parent().unwrap()).unwrap();
}

#[test]
fn invalid_project_manifest_returns_structured_failure() {
    let destination = temporary_directory("invalid");
    fs::create_dir_all(destination.join("src")).unwrap();
    fs::write(
        destination.join("sprout.toml"),
        "manifest_version = 99\n[project]\nname = \"Invalid\"\nversion = \"0.1.0\"\nentry = \"src/main.spr\"\n",
    )
    .unwrap();
    fs::write(destination.join("src/main.spr"), "print(1)\n").unwrap();
    let output = Command::new(env!("CARGO_BIN_EXE_sprout"))
        .args(["check", destination.to_str().unwrap()])
        .output()
        .unwrap();
    assert_eq!(output.status.code(), Some(1));
    let stderr = String::from_utf8(output.stderr).unwrap();
    assert!(stderr.contains("error[SPP003]"), "{stderr}");
    assert!(
        stderr.contains("Unsupported manifest version 99"),
        "{stderr}"
    );
    fs::remove_dir_all(destination).unwrap();
}
