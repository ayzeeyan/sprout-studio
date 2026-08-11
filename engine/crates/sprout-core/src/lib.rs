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

/// Parses and executes one source file through the canonical language pipeline.
pub fn execute(source: &SourceFile) -> ExecutionResult {
    let parsed = parser::parse(source);
    let Some(program) = parsed.program else {
        return ExecutionResult {
            output: Vec::new(),
            diagnostics: parsed.diagnostics,
        };
    };
    let executed = runtime::run(&program);
    ExecutionResult {
        output: executed.output,
        diagnostics: executed.diagnostic.into_iter().collect(),
    }
}
