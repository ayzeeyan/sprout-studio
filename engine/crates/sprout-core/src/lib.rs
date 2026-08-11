//! The dependency-free language engine for Sprout.
//!
//! The public modules mirror the language pipeline. Keeping them independent
//! from the CLI and any future GUI allows every frontend to share one engine.

pub mod ast;
pub mod diagnostic;
pub mod formatter;
pub mod ide;
pub mod interop;
pub mod lexer;
pub mod parser;
pub mod runtime;
pub mod semantic;
pub mod source;
pub mod token;

use diagnostic::Diagnostic;
pub use parser::SyntaxProfile;
use source::SourceFile;

/// High-level engine response suitable for CLI, FFI, and editor adapters.
pub struct ExecutionResult {
    pub output: Vec<String>,
    pub diagnostics: Vec<Diagnostic>,
    pub canvas_commands: Vec<runtime::CanvasCommand>,
}

pub struct DebugExecutionResult {
    pub output: Vec<String>,
    pub diagnostics: Vec<Diagnostic>,
    pub canvas_commands: Vec<runtime::CanvasCommand>,
    pub snapshots: Vec<runtime::DebugSnapshot>,
}

pub struct CheckResult {
    pub diagnostics: Vec<Diagnostic>,
}

/// Parses and semantically analyzes one source file without executing it.
pub fn check(source: &SourceFile) -> CheckResult {
    check_profile(source, SyntaxProfile::Guided)
}

pub fn check_profile(source: &SourceFile, profile: SyntaxProfile) -> CheckResult {
    let parsed = parser::parse_profile(source, profile);
    let Some(program) = parsed.program else {
        return CheckResult {
            diagnostics: parsed.diagnostics,
        };
    };
    CheckResult {
        diagnostics: semantic::analyze(&program).diagnostics,
    }
}

/// Parses and executes one source file through the canonical language pipeline.
pub fn execute(source: &SourceFile) -> ExecutionResult {
    execute_profile(source, SyntaxProfile::Guided)
}

pub fn execute_profile(source: &SourceFile, profile: SyntaxProfile) -> ExecutionResult {
    execute_profile_with_runtime(source, profile, runtime::run)
}

pub fn execute_profile_with_python(
    source: &SourceFile,
    profile: SyntaxProfile,
    python: &mut dyn interop::PythonHost,
) -> ExecutionResult {
    execute_profile_with_runtime(source, profile, |program| {
        runtime::run_with_python(program, python)
    })
}

fn execute_profile_with_runtime(
    source: &SourceFile,
    profile: SyntaxProfile,
    run: impl FnOnce(&ast::Program) -> runtime::RunResult,
) -> ExecutionResult {
    let parsed = parser::parse_profile(source, profile);
    let Some(program) = parsed.program else {
        return ExecutionResult {
            output: Vec::new(),
            diagnostics: parsed.diagnostics,
            canvas_commands: Vec::new(),
        };
    };
    let analyzed = semantic::analyze(&program);
    if !analyzed.diagnostics.is_empty() {
        return ExecutionResult {
            output: Vec::new(),
            diagnostics: analyzed.diagnostics,
            canvas_commands: Vec::new(),
        };
    }
    let executed = run(&program);
    ExecutionResult {
        output: executed.output,
        diagnostics: executed.diagnostic.into_iter().collect(),
        canvas_commands: executed.canvas_commands,
    }
}

pub fn debug_profile_with_python(
    source: &SourceFile,
    profile: SyntaxProfile,
    python: &mut dyn interop::PythonHost,
) -> DebugExecutionResult {
    let parsed = parser::parse_profile(source, profile);
    let Some(program) = parsed.program else {
        return DebugExecutionResult {
            output: Vec::new(),
            diagnostics: parsed.diagnostics,
            canvas_commands: Vec::new(),
            snapshots: Vec::new(),
        };
    };
    let analyzed = semantic::analyze(&program);
    if !analyzed.diagnostics.is_empty() {
        return DebugExecutionResult {
            output: Vec::new(),
            diagnostics: analyzed.diagnostics,
            canvas_commands: Vec::new(),
            snapshots: Vec::new(),
        };
    }
    let executed = runtime::run_debug_with_python(&program, python);
    DebugExecutionResult {
        output: executed.output,
        diagnostics: executed.diagnostic.into_iter().collect(),
        canvas_commands: executed.canvas_commands,
        snapshots: executed.snapshots,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn semantic_failures_prevent_all_execution() {
        let source = SourceFile::new("test.spr", "print(\"before\")\nprint(missing)\n");
        let result = execute(&source);
        assert!(result.output.is_empty());
        assert_eq!(result.diagnostics[0].code, "SPT201");
    }

    #[test]
    fn every_profile_executes_through_the_same_engine_pipeline() {
        let cases = [
            (SyntaxProfile::Guided, "has value = 3\nprint(value)\n"),
            (SyntaxProfile::Structured, "int value = 3;\nprint(value);\n"),
            (SyntaxProfile::Advanced, "value := 3\nprint(value)\n"),
        ];
        for (profile, text) in cases {
            let result = execute_profile(&SourceFile::new("test.spr", text), profile);
            assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
            assert_eq!(result.output, vec!["3"]);
        }
    }
}
