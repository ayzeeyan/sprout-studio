//! Static semantic analysis over the canonical AST.

use crate::ast::{Expression, ExpressionKind, Program, Statement, StatementKind};
use crate::diagnostic::Diagnostic;
use crate::source::SourceSpan;
use std::collections::HashMap;

#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Type {
    Int,
    Float,
    Bool,
    String,
    Nil,
    Function(FunctionType),
    Unknown,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct FunctionType {
    pub arity: Option<usize>,
    pub return_type: Box<Type>,
}

#[derive(Clone, Debug)]
struct Symbol {
    kind: SymbolKind,
    declared_at: SourceSpan,
}

#[derive(Clone, Debug)]
enum SymbolKind {
    Variable(Type),
    Function(FunctionType),
}

impl SymbolKind {
    fn ty(&self) -> Type {
        match self {
            Self::Variable(ty) => ty.clone(),
            Self::Function(function) => Type::Function(function.clone()),
        }
    }
}

#[derive(Default)]
struct Scope {
    symbols: HashMap<String, Symbol>,
}

pub struct AnalysisResult {
    pub diagnostics: Vec<Diagnostic>,
}

/// Resolves names and checks the statically knowable rules of a program.
pub fn analyze(program: &Program) -> AnalysisResult {
    Analyzer::new().analyze(program)
}

struct Analyzer {
    scopes: Vec<Scope>,
    diagnostics: Vec<Diagnostic>,
    function_depth: usize,
}

impl Analyzer {
    fn new() -> Self {
        let mut global = Scope::default();
        global.symbols.insert(
            "print".into(),
            Symbol {
                kind: SymbolKind::Function(FunctionType {
                    arity: None,
                    return_type: Box::new(Type::Nil),
                }),
                declared_at: SourceSpan::new("<builtin>", 0, 0, 1, 1, 1, 1),
            },
        );
        Self {
            scopes: vec![global],
            diagnostics: Vec::new(),
            function_depth: 0,
        }
    }

    fn analyze(mut self, program: &Program) -> AnalysisResult {
        self.statements(&program.statements);
        AnalysisResult {
            diagnostics: self.diagnostics,
        }
    }

    fn statements(&mut self, statements: &[Statement]) {
        for statement in statements {
            self.statement(statement);
        }
    }

    fn statement(&mut self, statement: &Statement) {
        match &statement.kind {
            StatementKind::Variable { name, initializer } => {
                let ty = self.expression(initializer);
                self.declare(name, SymbolKind::Variable(ty), &statement.span);
            }
            StatementKind::Assignment { name, value } => {
                self.expression(value);
                if self.resolve(name).is_none() {
                    self.unknown_symbol(name, &statement.span);
                }
            }
            StatementKind::Expression(expression) => {
                self.expression(expression);
            }
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            } => {
                self.expression(condition);
                self.nested(then_branch);
                self.nested(else_branch);
            }
            StatementKind::Repeat { count, body } => {
                self.expression(count);
                self.nested(body);
            }
            StatementKind::While { condition, body } => {
                self.expression(condition);
                self.nested(body);
            }
            StatementKind::Function {
                name,
                parameters,
                body,
            } => {
                let signature = FunctionType {
                    arity: Some(parameters.len()),
                    return_type: Box::new(Type::Unknown),
                };
                self.declare(name, SymbolKind::Function(signature), &statement.span);
                self.push_scope();
                self.function_depth += 1;
                for parameter in parameters {
                    self.declare(
                        parameter,
                        SymbolKind::Variable(Type::Unknown),
                        &statement.span,
                    );
                }
                self.statements(body);
                self.function_depth -= 1;
                self.pop_scope();
            }
            StatementKind::Return(value) => {
                if self.function_depth == 0 {
                    self.diagnostics.push(Diagnostic::error(
                        "SPT213",
                        "Return outside an action",
                        "`return` can only be used inside an action.",
                        statement.span.clone(),
                    ));
                }
                if let Some(value) = value {
                    self.expression(value);
                }
            }
        }
    }

    fn expression(&mut self, expression: &Expression) -> Type {
        match &expression.kind {
            ExpressionKind::Integer(_) => Type::Int,
            ExpressionKind::Float(_) => Type::Float,
            ExpressionKind::Bool(_) => Type::Bool,
            ExpressionKind::String(_) => Type::String,
            ExpressionKind::Nil => Type::Nil,
            ExpressionKind::Variable(name) => self
                .resolve(name)
                .map(|symbol| symbol.kind.ty())
                .unwrap_or_else(|| {
                    self.unknown_symbol(name, &expression.span);
                    Type::Unknown
                }),
            ExpressionKind::Unary { operand, .. } => self.expression(operand),
            ExpressionKind::Binary { left, right, .. } => {
                let left = self.expression(left);
                self.expression(right);
                left
            }
            ExpressionKind::Call { callee, arguments } => {
                let callee = self.expression(callee);
                for argument in arguments {
                    self.expression(argument);
                }
                match callee {
                    Type::Function(function) => *function.return_type,
                    _ => Type::Unknown,
                }
            }
        }
    }

    fn nested(&mut self, statements: &[Statement]) {
        self.push_scope();
        self.statements(statements);
        self.pop_scope();
    }

    fn declare(&mut self, name: &str, kind: SymbolKind, span: &SourceSpan) {
        let current = self.scopes.last_mut().expect("analyzer always has a scope");
        if let Some(previous) = current.symbols.get(name) {
            let mut diagnostic = Diagnostic::error(
                "SPT210",
                "Duplicate declaration",
                format!("`{name}` is already declared in this scope."),
                span.clone(),
            );
            diagnostic.suggestions.push(format!(
                "The earlier declaration is at {}:{}:{}.",
                previous.declared_at.file,
                previous.declared_at.start_line,
                previous.declared_at.start_column
            ));
            self.diagnostics.push(diagnostic);
            return;
        }
        current.symbols.insert(
            name.to_owned(),
            Symbol {
                kind,
                declared_at: span.clone(),
            },
        );
    }

    fn resolve(&self, name: &str) -> Option<&Symbol> {
        self.scopes
            .iter()
            .rev()
            .find_map(|scope| scope.symbols.get(name))
    }

    fn unknown_symbol(&mut self, name: &str, span: &SourceSpan) {
        let mut diagnostic = Diagnostic::error(
            "SPT201",
            "Unknown symbol",
            format!("`{name}` has not been declared in this scope."),
            span.clone(),
        );
        if let Some(candidate) = self.closest_visible_name(name) {
            diagnostic
                .suggestions
                .push(format!("Did you mean `{candidate}`?"));
        }
        self.diagnostics.push(diagnostic);
    }

    fn closest_visible_name(&self, requested: &str) -> Option<String> {
        self.scopes
            .iter()
            .rev()
            .flat_map(|scope| scope.symbols.keys())
            .filter_map(|candidate| {
                let distance = edit_distance(requested, candidate);
                let limit = 2.max(requested.len() / 3);
                (distance <= limit).then_some((distance, candidate))
            })
            .min_by(|left, right| left.0.cmp(&right.0).then_with(|| left.1.cmp(right.1)))
            .map(|(_, candidate)| candidate.clone())
    }

    fn push_scope(&mut self) {
        self.scopes.push(Scope::default());
    }

    fn pop_scope(&mut self) {
        self.scopes.pop();
    }
}

fn edit_distance(left: &str, right: &str) -> usize {
    let right: Vec<char> = right.chars().collect();
    let mut previous: Vec<usize> = (0..=right.len()).collect();
    for (left_index, left_char) in left.chars().enumerate() {
        let mut current = vec![left_index + 1];
        for (right_index, right_char) in right.iter().enumerate() {
            let insertion = current[right_index] + 1;
            let deletion = previous[right_index + 1] + 1;
            let substitution = previous[right_index] + usize::from(left_char != *right_char);
            current.push(insertion.min(deletion).min(substitution));
        }
        previous = current;
    }
    previous[right.len()]
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::parser::parse;
    use crate::source::SourceFile;

    fn analyze_source(text: &str) -> AnalysisResult {
        let parsed = parse(&SourceFile::new("test.spr", text));
        assert!(parsed.diagnostics.is_empty(), "{:?}", parsed.diagnostics);
        analyze(&parsed.program.unwrap())
    }

    #[test]
    fn resolves_nested_lexical_scopes() {
        let result = analyze_source(
            "has outer = 1\naction use(value)\n  if true\n    print(outer + value)\n  end\nend\n",
        );
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
    }

    #[test]
    fn reports_duplicate_declaration_in_same_scope() {
        let result = analyze_source("has score = 1\nhas score = 2\n");
        assert_eq!(result.diagnostics[0].code, "SPT210");
        assert_eq!(result.diagnostics[0].span.start_line, 2);
    }

    #[test]
    fn reports_unknown_symbol_with_real_visible_suggestion() {
        let result = analyze_source("has attack = 1\nprint(attck)\n");
        assert_eq!(result.diagnostics[0].code, "SPT201");
        assert_eq!(
            result.diagnostics[0].suggestions,
            vec!["Did you mean `attack`?"]
        );
    }

    #[test]
    fn rejects_return_outside_action() {
        let result = analyze_source("return 1\n");
        assert_eq!(result.diagnostics[0].code, "SPT213");
    }
}
