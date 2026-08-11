//! Static semantic analysis over the canonical AST.

use crate::ast::{
    BinaryOperator, Expression, ExpressionKind, Program, Statement, StatementKind, UnaryOperator,
};
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
    Class(String),
    Instance(String),
    Unknown,
}

impl Type {
    fn name(&self) -> String {
        match self {
            Self::Int => "Int".into(),
            Self::Float => "Float".into(),
            Self::Bool => "Bool".into(),
            Self::String => "String".into(),
            Self::Nil => "Nil".into(),
            Self::Function(_) => "Function".into(),
            Self::Class(name) => format!("class {name}"),
            Self::Instance(name) => name.clone(),
            Self::Unknown => "Unknown".into(),
        }
    }
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
    Class(String),
}

impl SymbolKind {
    fn ty(&self) -> Type {
        match self {
            Self::Variable(ty) => ty.clone(),
            Self::Function(function) => Type::Function(function.clone()),
            Self::Class(name) => Type::Class(name.clone()),
        }
    }
}

#[derive(Default)]
struct Scope {
    symbols: HashMap<String, Symbol>,
}

#[derive(Default)]
struct ClassInfo {
    methods: HashMap<String, FunctionType>,
}

pub struct AnalysisResult {
    pub diagnostics: Vec<Diagnostic>,
}

/// Resolves names and validates all statically knowable Phase 2 rules.
pub fn analyze(program: &Program) -> AnalysisResult {
    Analyzer::new().analyze(program)
}

struct Analyzer {
    scopes: Vec<Scope>,
    classes: HashMap<String, ClassInfo>,
    diagnostics: Vec<Diagnostic>,
    return_types: Vec<Vec<Type>>,
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
            classes: HashMap::new(),
            diagnostics: Vec::new(),
            return_types: Vec::new(),
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
                let value_type = self.expression(value);
                if let Some(symbol) = self.resolve(name).cloned() {
                    self.require_compatible(&symbol.kind.ty(), &value_type, &value.span);
                } else {
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
                let condition_type = self.expression(condition);
                self.require_type(
                    &condition_type,
                    &Type::Bool,
                    &condition.span,
                    "an `if` condition",
                );
                self.nested(then_branch);
                self.nested(else_branch);
            }
            StatementKind::Repeat { count, body } => {
                let count_type = self.expression(count);
                self.require_type(&count_type, &Type::Int, &count.span, "a repeat count");
                self.nested(body);
            }
            StatementKind::While { condition, body } => {
                let condition_type = self.expression(condition);
                self.require_type(
                    &condition_type,
                    &Type::Bool,
                    &condition.span,
                    "a `while` condition",
                );
                self.nested(body);
            }
            StatementKind::Function {
                name,
                parameters,
                body,
            } => {
                self.function(name, parameters, body, &statement.span, true, None);
            }
            StatementKind::Class { name, methods } => self.class(name, methods, &statement.span),
            StatementKind::Return(value) => {
                if self.return_types.is_empty() {
                    self.diagnostics.push(Diagnostic::error(
                        "SPT213",
                        "Return outside an action",
                        "`return` can only be used inside an action.",
                        statement.span.clone(),
                    ));
                }
                let ty = value
                    .as_ref()
                    .map(|expression| self.expression(expression))
                    .unwrap_or(Type::Nil);
                if let Some(returns) = self.return_types.last_mut() {
                    returns.push(ty);
                }
            }
        }
    }

    fn function(
        &mut self,
        name: &str,
        parameters: &[String],
        body: &[Statement],
        span: &SourceSpan,
        declare: bool,
        self_type: Option<Type>,
    ) -> Type {
        let signature = FunctionType {
            arity: Some(parameters.len()),
            return_type: Box::new(Type::Unknown),
        };
        if declare {
            self.declare(name, SymbolKind::Function(signature), span);
        }
        self.push_scope();
        if let Some(self_type) = self_type {
            self.declare("self", SymbolKind::Variable(self_type), span);
        }
        for parameter in parameters {
            self.declare(parameter, SymbolKind::Variable(Type::Unknown), span);
        }
        self.return_types.push(Vec::new());
        self.statements(body);
        let returns = self
            .return_types
            .pop()
            .expect("function return frame exists");
        self.pop_scope();
        let return_type = self.merge_return_types(name, &returns, span);

        if declare
            && let Some(symbol) = self
                .scopes
                .last_mut()
                .and_then(|scope| scope.symbols.get_mut(name))
            && let SymbolKind::Function(function) = &mut symbol.kind
        {
            *function.return_type = return_type.clone();
        }
        return_type
    }

    fn class(&mut self, class_name: &str, methods: &[Statement], span: &SourceSpan) {
        self.declare(class_name, SymbolKind::Class(class_name.to_owned()), span);
        let mut info = ClassInfo::default();
        for method in methods {
            if let StatementKind::Function {
                name: method_name,
                parameters,
                ..
            } = &method.kind
            {
                if info.methods.contains_key(method_name) {
                    self.diagnostics.push(Diagnostic::error(
                        "SPT210",
                        "Duplicate member",
                        format!(
                            "Class `{class_name}` already contains an action named `{method_name}`."
                        ),
                        method.span.clone(),
                    ));
                } else {
                    info.methods.insert(
                        method_name.clone(),
                        FunctionType {
                            arity: Some(parameters.len()),
                            return_type: Box::new(Type::Unknown),
                        },
                    );
                }
            }
        }
        self.classes.insert(class_name.to_owned(), info);

        for method in methods {
            if let StatementKind::Function {
                name: method_name,
                parameters,
                body,
            } = &method.kind
            {
                let return_type = self.function(
                    method_name,
                    parameters,
                    body,
                    &method.span,
                    false,
                    Some(Type::Instance(class_name.to_owned())),
                );
                if let Some(method) = self
                    .classes
                    .get_mut(class_name)
                    .and_then(|class| class.methods.get_mut(method_name))
                {
                    *method.return_type = return_type;
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
            ExpressionKind::Unary { operator, operand } => {
                let operand_type = self.expression(operand);
                match operator {
                    UnaryOperator::Negate => {
                        if !is_numeric(&operand_type) && operand_type != Type::Unknown {
                            self.invalid_operator("-", &operand_type, None, &expression.span);
                            Type::Unknown
                        } else {
                            operand_type
                        }
                    }
                    UnaryOperator::Not => Type::Bool,
                }
            }
            ExpressionKind::Binary {
                left,
                operator,
                right,
            } => {
                let left_type = self.expression(left);
                let right_type = self.expression(right);
                self.binary_type(&left_type, *operator, &right_type, &expression.span)
            }
            ExpressionKind::Call { callee, arguments } => {
                let callee_type = self.expression(callee);
                for argument in arguments {
                    self.expression(argument);
                }
                match callee_type {
                    Type::Function(function) => {
                        if let Some(expected) = function.arity
                            && expected != arguments.len()
                        {
                            self.diagnostics.push(Diagnostic::error(
                                "SPT212",
                                "Wrong number of arguments",
                                format!(
                                    "This action expects {expected} argument(s), but received {}.",
                                    arguments.len()
                                ),
                                expression.span.clone(),
                            ));
                        }
                        *function.return_type
                    }
                    Type::Class(name) => {
                        if !arguments.is_empty() {
                            self.diagnostics.push(Diagnostic::error(
                                "SPT212",
                                "Wrong number of arguments",
                                "Phase 2 class constructors do not accept arguments.",
                                expression.span.clone(),
                            ));
                        }
                        Type::Instance(name)
                    }
                    Type::Unknown => Type::Unknown,
                    other => {
                        self.diagnostics.push(Diagnostic::error(
                            "SPT206",
                            "Value is not callable",
                            format!("A value of type `{}` cannot be called.", other.name()),
                            callee.span.clone(),
                        ));
                        Type::Unknown
                    }
                }
            }
            ExpressionKind::Member {
                object,
                name,
                member_span,
            } => {
                let object_type = self.expression(object);
                let Type::Instance(class_name) = object_type else {
                    if object_type != Type::Unknown {
                        self.diagnostics.push(Diagnostic::error(
                            "SPT204",
                            "Unknown member",
                            format!(
                                "Values of type `{}` do not expose members.",
                                object_type.name()
                            ),
                            member_span.clone(),
                        ));
                    }
                    return Type::Unknown;
                };
                if let Some(method) = self
                    .classes
                    .get(&class_name)
                    .and_then(|class| class.methods.get(name))
                    .cloned()
                {
                    Type::Function(method)
                } else {
                    let mut diagnostic = Diagnostic::error(
                        "SPT204",
                        "Unknown member",
                        format!("`{class_name}` has no action named `{name}`."),
                        member_span.clone(),
                    );
                    if let Some(candidate) = self.closest_member(&class_name, name) {
                        diagnostic
                            .suggestions
                            .push(format!("Did you mean `{candidate}`?"));
                    }
                    self.diagnostics.push(diagnostic);
                    Type::Unknown
                }
            }
        }
    }

    fn binary_type(
        &mut self,
        left: &Type,
        operator: BinaryOperator,
        right: &Type,
        span: &SourceSpan,
    ) -> Type {
        use BinaryOperator as Op;
        if left == &Type::Unknown || right == &Type::Unknown {
            return Type::Unknown;
        }
        match operator {
            Op::Add if left == &Type::String && right == &Type::String => Type::String,
            Op::Add | Op::Subtract | Op::Multiply | Op::Divide | Op::Remainder
                if is_numeric(left) && is_numeric(right) =>
            {
                if left == &Type::Float || right == &Type::Float {
                    Type::Float
                } else {
                    Type::Int
                }
            }
            Op::Less | Op::LessEqual | Op::Greater | Op::GreaterEqual
                if is_numeric(left) && is_numeric(right) =>
            {
                Type::Bool
            }
            Op::Equal | Op::NotEqual if compatible(left, right) => Type::Bool,
            Op::And | Op::Or if left == &Type::Bool && right == &Type::Bool => Type::Bool,
            _ => {
                self.invalid_operator(operator_text(operator), left, Some(right), span);
                Type::Unknown
            }
        }
    }

    fn merge_return_types(&mut self, name: &str, returns: &[Type], span: &SourceSpan) -> Type {
        let mut inferred: Option<Type> = None;
        for ty in returns {
            let Some(current) = inferred.as_ref() else {
                inferred = Some(ty.clone());
                continue;
            };
            if !compatible(current, ty) {
                self.diagnostics.push(Diagnostic::error(
                    "SPT214",
                    "Inconsistent return types",
                    format!(
                        "Action `{name}` returns both `{}` and `{}` values.",
                        current.name(),
                        ty.name()
                    ),
                    span.clone(),
                ));
                return Type::Unknown;
            } else if is_numeric(current) && is_numeric(ty) {
                inferred = Some(if current == &Type::Float || ty == &Type::Float {
                    Type::Float
                } else {
                    Type::Int
                });
            }
        }
        inferred.unwrap_or(Type::Nil)
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

    fn require_type(&mut self, actual: &Type, expected: &Type, span: &SourceSpan, role: &str) {
        if actual != &Type::Unknown && !compatible(expected, actual) {
            self.diagnostics.push(Diagnostic::error(
                "SPT211",
                "Type mismatch",
                format!(
                    "Expected {role} to be `{}`, but found `{}`.",
                    expected.name(),
                    actual.name()
                ),
                span.clone(),
            ));
        }
    }

    fn require_compatible(&mut self, expected: &Type, actual: &Type, span: &SourceSpan) {
        if !compatible(expected, actual) {
            self.diagnostics.push(Diagnostic::error(
                "SPT211",
                "Type mismatch",
                format!(
                    "Cannot assign a `{}` value to a `{}` variable.",
                    actual.name(),
                    expected.name()
                ),
                span.clone(),
            ));
        }
    }

    fn invalid_operator(
        &mut self,
        operator: &str,
        left: &Type,
        right: Option<&Type>,
        span: &SourceSpan,
    ) {
        let message = right.map_or_else(
            || {
                format!(
                    "Operator `{operator}` cannot be applied to `{}`.",
                    left.name()
                )
            },
            |right| {
                format!(
                    "Operator `{operator}` cannot be applied to `{}` and `{}`.",
                    left.name(),
                    right.name()
                )
            },
        );
        self.diagnostics.push(Diagnostic::error(
            "SPT215",
            "Invalid operator",
            message,
            span.clone(),
        ));
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
        closest_name(
            requested,
            self.scopes
                .iter()
                .rev()
                .flat_map(|scope| scope.symbols.keys()),
        )
    }

    fn closest_member(&self, class_name: &str, requested: &str) -> Option<String> {
        self.classes
            .get(class_name)
            .and_then(|class| closest_name(requested, class.methods.keys()))
    }

    fn push_scope(&mut self) {
        self.scopes.push(Scope::default());
    }

    fn pop_scope(&mut self) {
        self.scopes.pop();
    }
}

fn compatible(expected: &Type, actual: &Type) -> bool {
    expected == &Type::Unknown
        || actual == &Type::Unknown
        || expected == actual
        || (is_numeric(expected) && is_numeric(actual))
}

fn is_numeric(ty: &Type) -> bool {
    matches!(ty, Type::Int | Type::Float)
}

fn operator_text(operator: BinaryOperator) -> &'static str {
    match operator {
        BinaryOperator::Add => "+",
        BinaryOperator::Subtract => "-",
        BinaryOperator::Multiply => "*",
        BinaryOperator::Divide => "/",
        BinaryOperator::Remainder => "%",
        BinaryOperator::Equal => "==",
        BinaryOperator::NotEqual => "!=",
        BinaryOperator::Less => "<",
        BinaryOperator::LessEqual => "<=",
        BinaryOperator::Greater => ">",
        BinaryOperator::GreaterEqual => ">=",
        BinaryOperator::And => "and",
        BinaryOperator::Or => "or",
    }
}

fn closest_name<'a>(
    requested: &str,
    candidates: impl Iterator<Item = &'a String>,
) -> Option<String> {
    candidates
        .filter_map(|candidate| {
            let distance = edit_distance(requested, candidate);
            let limit = 2.max(requested.chars().count() / 3);
            (distance <= limit).then_some((distance, candidate))
        })
        .min_by(|left, right| left.0.cmp(&right.0).then_with(|| left.1.cmp(right.1)))
        .map(|(_, candidate)| candidate.clone())
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

    #[test]
    fn reports_unknown_member_with_exact_span_and_valid_suggestion() {
        let result = analyze_source(
            "class Player\n  action attack(enemy)\n    print(enemy)\n  end\nend\nhas player = Player()\nhas enemy = nil\nplayer.attck(enemy)\n",
        );
        let diagnostic = &result.diagnostics[0];
        assert_eq!(diagnostic.code, "SPT204");
        assert_eq!(diagnostic.span.start_line, 8);
        assert_eq!(diagnostic.span.start_column, 8);
        assert_eq!(diagnostic.span.end_column, 13);
        assert_eq!(diagnostic.suggestions, vec!["Did you mean `attack`?"]);
    }

    #[test]
    fn validates_function_arity() {
        let result = analyze_source("action add(a, b)\n  return a\nend\nprint(add(1))\n");
        assert_eq!(result.diagnostics[0].code, "SPT212");
    }

    #[test]
    fn validates_assignment_and_operator_types() {
        let assignment = analyze_source("has score = 1\nscore = \"many\"\n");
        assert_eq!(assignment.diagnostics[0].code, "SPT211");
        let operator = analyze_source("print(true + 1)\n");
        assert_eq!(operator.diagnostics[0].code, "SPT215");
    }

    #[test]
    fn rejects_inconsistent_return_types() {
        let result = analyze_source(
            "action choose(flag)\n  if flag\n    return 1\n  else\n    return \"one\"\n  end\nend\n",
        );
        assert_eq!(result.diagnostics[0].code, "SPT214");
    }

    #[test]
    fn infers_method_return_type_for_following_expressions() {
        let result = analyze_source(
            "class Answer\n  action text()\n    return \"one\"\n  end\nend\nhas answer = Answer()\nprint(answer.text() - 1)\n",
        );
        assert_eq!(result.diagnostics[0].code, "SPT215");
    }
}
