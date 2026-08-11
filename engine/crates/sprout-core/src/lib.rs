//! The dependency-free language engine for Sprout.
//!
//! The public modules mirror the language pipeline. Keeping them independent
//! from the CLI and any future GUI allows every frontend to share one engine.

pub mod ast;
pub mod diagnostic;
pub mod lexer;
pub mod parser;
pub mod runtime;
pub mod semantic;
pub mod source;
pub mod token;

use diagnostic::Diagnostic;
use source::SourceFile;

/// High-level engine response suitable for CLI, FFI, and editor adapters.
pub struct ExecutionResult {
    pub output: Vec<String>,
    pub diagnostics: Vec<Diagnostic>,
}

pub struct CheckResult {
    pub diagnostics: Vec<Diagnostic>,
}

/// Parses and semantically analyzes one source file without executing it.
pub fn check(source: &SourceFile) -> CheckResult {
    let parsed = parser::parse(source);
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
    let parsed = parser::parse(source);
    let Some(program) = parsed.program else {
        return ExecutionResult {
            output: Vec::new(),
            diagnostics: parsed.diagnostics,
        };
    };
    let analyzed = semantic::analyze(&program);
    if !analyzed.diagnostics.is_empty() {
        return ExecutionResult {
            output: Vec::new(),
            diagnostics: analyzed.diagnostics,
        };
    }
    let executed = runtime::run(&program);
    ExecutionResult {
        output: executed.output,
        diagnostics: executed.diagnostic.into_iter().collect(),
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
}
