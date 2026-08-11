//! The dependency-free language engine for Sprout.
//!
//! The public modules mirror the language pipeline. Keeping them independent
//! from the CLI and any future GUI allows every frontend to share one engine.

pub mod ast;
pub mod diagnostic;
pub mod lexer;
pub mod parser;
pub mod runtime;
pub mod source;
pub mod token;
