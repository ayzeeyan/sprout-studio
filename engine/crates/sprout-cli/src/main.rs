use sprout_core::SyntaxProfile;
use sprout_core::diagnostic::Diagnostic;
use sprout_core::source::SourceFile;
use sprout_project::{ProjectError, ProjectTemplate, create_project, load_project};
use sprout_python::{EmbeddedPythonHost, sync_project_packages};
use std::env;
use std::fs;
use std::path::{Path, PathBuf};
use std::process::ExitCode;
use std::str::FromStr;

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
        "new" => new_project(&arguments[1..]),
        "python" => python_command(&arguments[1..]),
        "run" | "check" => {
            let options = source_options(command, &arguments[1..])?;
            run_target(options, command == "run")
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

#[derive(Debug)]
struct SourceOptions {
    path: PathBuf,
    profile: Option<SyntaxProfile>,
}

fn source_options(command: &str, arguments: &[String]) -> Result<SourceOptions, u8> {
    let mut profile = None;
    let mut path = None;
    let mut index = 0;
    while index < arguments.len() {
        if arguments[index] == "--profile" {
            let Some(value) = arguments.get(index + 1) else {
                eprintln!("error: `--profile` expects guided, structured, or advanced");
                return Err(2);
            };
            profile = Some(parse_profile(value)?);
            index += 2;
        } else if arguments[index].starts_with('-') {
            eprintln!("error: unknown option `{}`", arguments[index]);
            return Err(2);
        } else if path.replace(PathBuf::from(&arguments[index])).is_some() {
            eprintln!("error: `{command}` expects at most one file or project path");
            return Err(2);
        } else {
            index += 1;
        }
    }
    Ok(SourceOptions {
        path: path.unwrap_or_else(|| PathBuf::from(".")),
        profile,
    })
}

fn parse_profile(value: &str) -> Result<SyntaxProfile, u8> {
    match value {
        "guided" => Ok(SyntaxProfile::Guided),
        "structured" => Ok(SyntaxProfile::Structured),
        "advanced" => Ok(SyntaxProfile::Advanced),
        _ => {
            eprintln!(
                "error: unknown syntax profile `{value}`; expected guided, structured, or advanced"
            );
            Err(2)
        }
    }
}

fn new_project(arguments: &[String]) -> Result<(), u8> {
    let mut name = None;
    let mut destination = None;
    let mut template = ProjectTemplate::Guided;
    let mut index = 0;
    while index < arguments.len() {
        match arguments[index].as_str() {
            "--template" => {
                let Some(value) = arguments.get(index + 1) else {
                    eprintln!("error: `--template` expects guided, structured, or advanced");
                    return Err(2);
                };
                template = ProjectTemplate::from_str(value).map_err(|error| {
                    render_project_error(&error);
                    2
                })?;
                index += 2;
            }
            "--path" => {
                let Some(value) = arguments.get(index + 1) else {
                    eprintln!("error: `--path` expects a destination directory");
                    return Err(2);
                };
                destination = Some(PathBuf::from(value));
                index += 2;
            }
            option if option.starts_with('-') => {
                eprintln!("error: unknown option `{option}`");
                return Err(2);
            }
            value if name.replace(value).is_some() => {
                eprintln!("error: `new` expects exactly one project name");
                return Err(2);
            }
            _ => index += 1,
        }
    }
    let Some(name) = name else {
        eprintln!("error: `new` expects a project name");
        return Err(2);
    };
    let destination = destination.unwrap_or_else(|| PathBuf::from(name));
    let project = create_project(&destination, name, template).map_err(|error| {
        render_project_error(&error);
        1
    })?;
    println!(
        "Created {} project `{}` at {}.",
        template.id(),
        project.manifest.project.name,
        project.root.display()
    );
    Ok(())
}

fn run_target(options: SourceOptions, execute: bool) -> Result<(), u8> {
    if options
        .path
        .extension()
        .and_then(|extension| extension.to_str())
        == Some("spr")
    {
        run_file(
            &options.path,
            execute,
            options.profile.unwrap_or(SyntaxProfile::Guided),
            None,
            None,
        )
    } else {
        let project = load_project(&options.path).map_err(|error| {
            render_project_error(&error);
            1
        })?;
        let profile = options.profile.unwrap_or(project.profile);
        run_file(
            &project.entry_path,
            execute,
            profile,
            Some(&project.manifest.project.name),
            Some(project.root.join(".sprout/python")),
        )
    }
}

fn run_file(
    path: &Path,
    execute: bool,
    profile: SyntaxProfile,
    project_name: Option<&str>,
    project_packages: Option<PathBuf>,
) -> Result<(), u8> {
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
        let mut python = EmbeddedPythonHost::new(project_packages).map_err(|error| {
            eprintln!("error: could not initialize embedded Python: {error}");
            1
        })?;
        let result = sprout_core::execute_profile_with_python(&source, profile, &mut python);
        if !result.diagnostics.is_empty() {
            render_diagnostics(&result.diagnostics, source.text());
            return Err(1);
        }
        for line in result.output {
            println!("{line}");
        }
    } else {
        let result = sprout_core::check_profile(&source, profile);
        if !result.diagnostics.is_empty() {
            render_diagnostics(&result.diagnostics, source.text());
            return Err(1);
        }
        match project_name {
            Some(name) => println!("Checked project `{name}` successfully."),
            None => println!("Checked {} successfully.", path.display()),
        }
    }
    Ok(())
}

fn python_command(arguments: &[String]) -> Result<(), u8> {
    let Some(subcommand) = arguments.first().map(String::as_str) else {
        eprintln!("error: `python` expects the `sync` subcommand");
        return Err(2);
    };
    if subcommand != "sync" {
        eprintln!("error: unknown Python subcommand `{subcommand}`; expected `sync`");
        return Err(2);
    }

    let mut path = None;
    let mut python = None;
    let mut index = 1;
    while index < arguments.len() {
        match arguments[index].as_str() {
            "--python" => {
                let Some(value) = arguments.get(index + 1) else {
                    eprintln!("error: `--python` expects an executable path");
                    return Err(2);
                };
                python = Some(PathBuf::from(value));
                index += 2;
            }
            option if option.starts_with('-') => {
                eprintln!("error: unknown option `{option}`");
                return Err(2);
            }
            value if path.replace(PathBuf::from(value)).is_some() => {
                eprintln!("error: `python sync` expects at most one project path");
                return Err(2);
            }
            _ => index += 1,
        }
    }

    let project = load_project(path.unwrap_or_else(|| PathBuf::from("."))).map_err(|error| {
        render_project_error(&error);
        1
    })?;
    let executable = if project.manifest.python.is_empty() {
        PathBuf::from("python")
    } else {
        python.or_else(configured_python_executable).ok_or_else(|| {
            eprintln!(
                "error: bundled Python was not found beside Sprout; use --python <path> only for a development build"
            );
            1
        })?
    };
    let result = sync_project_packages(&executable, &project.root, &project.manifest.python)
        .map_err(|error| {
            eprintln!("error: Python package sync failed: {error}");
            1
        })?;
    if result.exit_code != Some(0) {
        if !result.stdout.trim().is_empty() {
            eprintln!("{}", result.stdout.trim_end());
        }
        if !result.stderr.trim().is_empty() {
            eprintln!("{}", result.stderr.trim_end());
        }
        eprintln!("error: pip exited without successfully synchronizing the project");
        return Err(1);
    }
    println!(
        "Synchronized {} Python package{} into {}.",
        project.manifest.python.len(),
        if project.manifest.python.len() == 1 {
            ""
        } else {
            "s"
        },
        project.root.join(".sprout/python").display()
    );
    Ok(())
}

fn configured_python_executable() -> Option<PathBuf> {
    if let Some(path) = env::var_os("SPROUT_PYTHON_EXECUTABLE").map(PathBuf::from) {
        return Some(path);
    }
    let file_name = if cfg!(windows) {
        "python.exe"
    } else {
        "python3"
    };
    env::current_exe()
        .ok()
        .and_then(|executable| executable.parent().map(|parent| parent.join(file_name)))
        .filter(|path| path.is_file())
}

fn render_project_error(error: &ProjectError) {
    for issue in &error.issues {
        eprintln!("error[{}]: Invalid Sprout project", issue.code);
        if let Some(path) = &issue.path {
            eprintln!("  --> {}", path.display());
        }
        eprintln!("   = {}\n", issue.message);
    }
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
        "Sprout programming language\n\nUSAGE:\n    sprout <COMMAND> [OPTIONS]\n\nCOMMANDS:\n    new <name>          Create a validated Sprout project\n    run [path]          Run a .spr file or project (current directory by default)\n    check [path]        Check a .spr file or project without executing\n    python sync [path]  Install manifest packages into .sprout/python\n    help                Show this help\n\nNEW OPTIONS:\n    --template <NAME>   guided (default), structured, or advanced\n    --path <DIRECTORY>  Exact project destination\n\nRUN/CHECK OPTIONS:\n    --profile <PROFILE> Override guided, structured, or advanced syntax\n\nPYTHON SYNC OPTIONS:\n    --python <PATH>     Development-only Python executable override\n    -h, --help          Show this help\n    -V, --version       Show the version"
    );
}
