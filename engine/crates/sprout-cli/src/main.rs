use sprout_core::diagnostic::Diagnostic;
use sprout_core::source::SourceFile;
use std::env;
use std::fs;
use std::path::Path;
use std::process::ExitCode;

fn main() -> ExitCode {
    match command(env::args().skip(1).collect()) {
        Ok(()) => ExitCode::SUCCESS,
        Err(code) => ExitCode::from(code),
    }
}

fn command(arguments: Vec<String>) -> Result<(), u8> {
    let Some(command) = arguments.first().map(String::as_str) else {
        print_help();
        return Err(2);
    };

    match command {
        "run" | "check" => {
            if arguments.len() != 2 {
                eprintln!("error: `{command}` expects exactly one .spr file\n");
                print_help();
                return Err(2);
            }
            run_file(Path::new(&arguments[1]), command == "run")
        }
        "--help" | "-h" | "help" => {
            print_help();
            Ok(())
        }
        "--version" | "-V" => {
            println!("sprout {}", env!("CARGO_PKG_VERSION"));
            Ok(())
        }
        unknown => {
            eprintln!("error: unknown command `{unknown}`\n");
            print_help();
            Err(2)
        }
    }
}

fn run_file(path: &Path, execute: bool) -> Result<(), u8> {
    if path.extension().and_then(|extension| extension.to_str()) != Some("spr") {
        eprintln!("error: expected a file with the .spr extension");
        return Err(2);
    }

    let text = fs::read_to_string(path).map_err(|error| {
        eprintln!("error: could not read {}: {error}", path.display());
        2
    })?;
    let source = SourceFile::new(path.display().to_string(), text);

    if execute {
        let result = sprout_core::execute(&source);
        if !result.diagnostics.is_empty() {
            render_diagnostics(&result.diagnostics, source.text());
            return Err(1);
        }
        for line in result.output {
            println!("{line}");
        }
    } else {
        let result = sprout_core::check(&source);
        if !result.diagnostics.is_empty() {
            render_diagnostics(&result.diagnostics, source.text());
            return Err(1);
        }
        println!("Checked {} successfully.", path.display());
    }
    Ok(())
}

fn render_diagnostics(diagnostics: &[Diagnostic], source: &str) {
    let lines: Vec<&str> = source.lines().collect();
    for diagnostic in diagnostics {
        eprintln!(
            "error[{}]: {}\n  --> {}:{}:{}\n   |",
            diagnostic.code,
            diagnostic.title,
            diagnostic.span.file,
            diagnostic.span.start_line,
            diagnostic.span.start_column
        );
        if let Some(line) = lines.get(diagnostic.span.start_line.saturating_sub(1)) {
            eprintln!("{:>3} | {line}", diagnostic.span.start_line);
            let width = if diagnostic.span.start_line == diagnostic.span.end_line {
                diagnostic
                    .span
                    .end_column
                    .saturating_sub(diagnostic.span.start_column)
                    .max(1)
            } else {
                1
            };
            eprintln!(
                "   | {}{} {}",
                " ".repeat(diagnostic.span.start_column.saturating_sub(1)),
                "^".repeat(width),
                diagnostic.message
            );
        } else {
            eprintln!("   = {}", diagnostic.message);
        }
        for suggestion in &diagnostic.suggestions {
            eprintln!("   = suggestion: {suggestion}");
        }
        eprintln!();
    }
}

fn print_help() {
    println!(
        "Sprout programming language\n\nUSAGE:\n    sprout <COMMAND> [FILE]\n\nCOMMANDS:\n    run <file.spr>      Parse and execute a program\n    check <file.spr>    Validate syntax without executing\n    help                Show this help\n\nOPTIONS:\n    -h, --help          Show this help\n    -V, --version       Show the version"
    );
}
