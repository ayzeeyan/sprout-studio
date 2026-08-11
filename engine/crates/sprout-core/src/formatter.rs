//! Canonical, profile-aware formatting for valid Sprout source.

use crate::ast::{
    BinaryOperator, Expression, ExpressionKind, Program, Statement, StatementKind, UnaryOperator,
};
use crate::diagnostic::Diagnostic;
use crate::parser::{SyntaxProfile, parse_profile};
use crate::source::{SourceFile, SourceSpan};
use std::fmt::Write;

pub struct FormatResult {
    pub formatted_source: Option<String>,
    pub diagnostics: Vec<Diagnostic>,
}

pub fn format_profile(source: &SourceFile, profile: SyntaxProfile) -> FormatResult {
    if let Some(span) = first_comment_span(source) {
        return FormatResult {
            formatted_source: None,
            diagnostics: vec![Diagnostic::error(
                "SPT400",
                "Formatting would lose a comment",
                "Comment-preserving formatting is not available yet; no source was changed.",
                span,
            )],
        };
    }
    let parsed = parse_profile(source, profile);
    let Some(program) = parsed.program else {
        return FormatResult {
            formatted_source: None,
            diagnostics: parsed.diagnostics,
        };
    };
    FormatResult {
        formatted_source: Some(Formatter::new(profile).program(&program)),
        diagnostics: Vec::new(),
    }
}

struct Formatter {
    profile: SyntaxProfile,
    output: String,
}

impl Formatter {
    fn new(profile: SyntaxProfile) -> Self {
        Self {
            profile,
            output: String::new(),
        }
    }

    fn program(mut self, program: &Program) -> String {
        self.statements(&program.statements, 0);
        if !self.output.ends_with('\n') {
            self.output.push('\n');
        }
        self.output
    }

    fn statements(&mut self, statements: &[Statement], indent: usize) {
        for statement in statements {
            self.statement(statement, indent);
        }
    }

    fn statement(&mut self, statement: &Statement, indent: usize) {
        match self.profile {
            SyntaxProfile::Guided => self.guided_statement(statement, indent),
            SyntaxProfile::Structured => self.structured_statement(statement, indent),
            SyntaxProfile::Advanced => self.advanced_statement(statement, indent),
        }
    }

    fn guided_statement(&mut self, statement: &Statement, indent: usize) {
        self.indent(indent);
        match &statement.kind {
            StatementKind::ImportPython { module, alias } => {
                writeln!(self.output, "import python {} as {alias}", quote(module)).unwrap();
            }
            StatementKind::Variable { name, initializer } => {
                writeln!(self.output, "has {name} = {}", expression(initializer, 0)).unwrap();
            }
            StatementKind::Assignment { name, value } => {
                writeln!(self.output, "{name} = {}", expression(value, 0)).unwrap();
            }
            StatementKind::Expression(value) => {
                writeln!(self.output, "{}", expression(value, 0)).unwrap();
            }
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            } => {
                writeln!(self.output, "if {}", expression(condition, 0)).unwrap();
                self.statements(then_branch, indent + 1);
                if !else_branch.is_empty() {
                    self.indent(indent);
                    self.output.push_str("else\n");
                    self.statements(else_branch, indent + 1);
                }
                self.indent(indent);
                self.output.push_str("end\n");
            }
            StatementKind::Repeat { count, body } => {
                writeln!(self.output, "repeat {} times", expression(count, 0)).unwrap();
                self.statements(body, indent + 1);
                self.indent(indent);
                self.output.push_str("end\n");
            }
            StatementKind::While { condition, body } => {
                writeln!(self.output, "while {}", expression(condition, 0)).unwrap();
                self.statements(body, indent + 1);
                self.indent(indent);
                self.output.push_str("end\n");
            }
            StatementKind::Function {
                name,
                parameters,
                body,
            } => {
                writeln!(self.output, "action {name}({})", parameters.join(", ")).unwrap();
                self.statements(body, indent + 1);
                self.indent(indent);
                self.output.push_str("end\n");
            }
            StatementKind::Class { name, methods } => {
                writeln!(self.output, "class {name}").unwrap();
                self.statements(methods, indent + 1);
                self.indent(indent);
                self.output.push_str("end\n");
            }
            StatementKind::Return(value) => {
                match value {
                    Some(value) => writeln!(self.output, "return {}", expression(value, 0)),
                    None => writeln!(self.output, "return"),
                }
                .unwrap();
            }
        }
    }

    fn structured_statement(&mut self, statement: &Statement, indent: usize) {
        self.indent(indent);
        match &statement.kind {
            StatementKind::ImportPython { module, alias } => {
                writeln!(self.output, "import python {} as {alias};", quote(module)).unwrap();
            }
            StatementKind::Variable { name, initializer } => {
                writeln!(
                    self.output,
                    "{} {name} = {};",
                    expression_type(initializer),
                    expression(initializer, 0)
                )
                .unwrap();
            }
            StatementKind::Assignment { name, value } => {
                writeln!(self.output, "{name} = {};", expression(value, 0)).unwrap();
            }
            StatementKind::Expression(value) => {
                writeln!(self.output, "{};", expression(value, 0)).unwrap();
            }
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            } => {
                writeln!(self.output, "if ({}) {{", expression(condition, 0)).unwrap();
                self.statements(then_branch, indent + 1);
                self.indent(indent);
                if else_branch.is_empty() {
                    self.output.push_str("}\n");
                } else {
                    self.output.push_str("} else {\n");
                    self.statements(else_branch, indent + 1);
                    self.indent(indent);
                    self.output.push_str("}\n");
                }
            }
            StatementKind::Repeat { count, body } => {
                writeln!(self.output, "repeat ({}) {{", expression(count, 0)).unwrap();
                self.statements(body, indent + 1);
                self.indent(indent);
                self.output.push_str("}\n");
            }
            StatementKind::While { condition, body } => {
                writeln!(self.output, "while ({}) {{", expression(condition, 0)).unwrap();
                self.statements(body, indent + 1);
                self.indent(indent);
                self.output.push_str("}\n");
            }
            StatementKind::Function {
                name,
                parameters,
                body,
            } => {
                let parameters = parameters
                    .iter()
                    .map(|parameter| format!("auto {parameter}"))
                    .collect::<Vec<_>>()
                    .join(", ");
                writeln!(self.output, "auto {name}({parameters}) {{").unwrap();
                self.statements(body, indent + 1);
                self.indent(indent);
                self.output.push_str("}\n");
            }
            StatementKind::Class { name, methods } => {
                writeln!(self.output, "class {name} {{").unwrap();
                self.statements(methods, indent + 1);
                self.indent(indent);
                self.output.push_str("}\n");
            }
            StatementKind::Return(value) => {
                match value {
                    Some(value) => writeln!(self.output, "return {};", expression(value, 0)),
                    None => writeln!(self.output, "return;"),
                }
                .unwrap();
            }
        }
    }

    fn advanced_statement(&mut self, statement: &Statement, indent: usize) {
        self.indent(indent);
        match &statement.kind {
            StatementKind::ImportPython { module, alias } => {
                writeln!(self.output, "import python {} as {alias}", quote(module)).unwrap();
            }
            StatementKind::Variable { name, initializer } => {
                writeln!(self.output, "{name} := {}", expression(initializer, 0)).unwrap();
            }
            StatementKind::Assignment { name, value } => {
                writeln!(self.output, "{name} = {}", expression(value, 0)).unwrap();
            }
            StatementKind::Expression(value) => {
                writeln!(self.output, "{}", expression(value, 0)).unwrap();
            }
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            } => {
                writeln!(self.output, "if {} =>", expression(condition, 0)).unwrap();
                self.statements(then_branch, indent + 1);
                if !else_branch.is_empty() {
                    self.indent(indent);
                    self.output.push_str("else =>\n");
                    self.statements(else_branch, indent + 1);
                }
            }
            StatementKind::Repeat { count, body } => {
                writeln!(self.output, "repeat {} =>", expression(count, 0)).unwrap();
                self.statements(body, indent + 1);
            }
            StatementKind::While { condition, body } => {
                writeln!(self.output, "while {} =>", expression(condition, 0)).unwrap();
                self.statements(body, indent + 1);
            }
            StatementKind::Function {
                name,
                parameters,
                body,
            } => {
                writeln!(self.output, "{name}({}) =>", parameters.join(", ")).unwrap();
                self.statements(body, indent + 1);
            }
            StatementKind::Class { name, methods } => {
                writeln!(self.output, "class {name} =>").unwrap();
                self.statements(methods, indent + 1);
            }
            StatementKind::Return(value) => {
                match value {
                    Some(value) => writeln!(self.output, "return {}", expression(value, 0)),
                    None => writeln!(self.output, "return"),
                }
                .unwrap();
            }
        }
    }

    fn indent(&mut self, level: usize) {
        self.output.push_str(&"    ".repeat(level));
    }
}

fn expression(value: &Expression, parent_precedence: u8) -> String {
    let precedence = expression_precedence(value);
    let rendered = match &value.kind {
        ExpressionKind::Integer(value) => value.to_string(),
        ExpressionKind::Float(value) => {
            let mut rendered = value.to_string();
            if !rendered.contains(['.', 'e', 'E']) {
                rendered.push_str(".0");
            }
            rendered
        }
        ExpressionKind::Bool(value) => value.to_string(),
        ExpressionKind::String(value) => quote(value),
        ExpressionKind::Nil => "nil".to_owned(),
        ExpressionKind::Variable(name) => name.clone(),
        ExpressionKind::Unary { operator, operand } => {
            let operator = match operator {
                UnaryOperator::Negate => "-",
                UnaryOperator::Not => "not ",
            };
            format!("{operator}{}", expression(operand, precedence))
        }
        ExpressionKind::Binary {
            left,
            operator,
            right,
        } => format!(
            "{} {} {}",
            expression(left, precedence),
            binary_text(*operator),
            expression(right, precedence + 1)
        ),
        ExpressionKind::Call { callee, arguments } => format!(
            "{}({})",
            expression(callee, precedence),
            arguments
                .iter()
                .map(|argument| expression(argument, 0))
                .collect::<Vec<_>>()
                .join(", ")
        ),
        ExpressionKind::Member { object, name, .. } => {
            format!("{}.{name}", expression(object, precedence))
        }
        ExpressionKind::Index { object, index } => format!(
            "{}[{}]",
            expression(object, precedence),
            expression(index, 0)
        ),
    };
    if precedence < parent_precedence {
        format!("({rendered})")
    } else {
        rendered
    }
}

fn expression_precedence(value: &Expression) -> u8 {
    match &value.kind {
        ExpressionKind::Binary { operator, .. } => match operator {
            BinaryOperator::Or => 1,
            BinaryOperator::And => 2,
            BinaryOperator::Equal | BinaryOperator::NotEqual => 3,
            BinaryOperator::Less
            | BinaryOperator::LessEqual
            | BinaryOperator::Greater
            | BinaryOperator::GreaterEqual => 4,
            BinaryOperator::Add | BinaryOperator::Subtract => 5,
            BinaryOperator::Multiply | BinaryOperator::Divide | BinaryOperator::Remainder => 6,
        },
        ExpressionKind::Unary { .. } => 7,
        ExpressionKind::Call { .. }
        | ExpressionKind::Member { .. }
        | ExpressionKind::Index { .. } => 8,
        _ => 9,
    }
}

fn binary_text(operator: BinaryOperator) -> &'static str {
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

fn expression_type(expression: &Expression) -> &'static str {
    match expression.kind {
        ExpressionKind::Integer(_) => "int",
        ExpressionKind::Float(_) => "float",
        ExpressionKind::Bool(_) => "bool",
        ExpressionKind::String(_) => "string",
        _ => "auto",
    }
}

fn quote(value: &str) -> String {
    let mut result = String::from("\"");
    for character in value.chars() {
        match character {
            '\\' => result.push_str("\\\\"),
            '"' => result.push_str("\\\""),
            '\n' => result.push_str("\\n"),
            '\r' => result.push_str("\\r"),
            '\t' => result.push_str("\\t"),
            character => result.push(character),
        }
    }
    result.push('"');
    result
}

fn first_comment_span(source: &SourceFile) -> Option<SourceSpan> {
    let mut quoted = false;
    let mut escaped = false;
    let mut line = 1;
    let mut column = 1;
    for (offset, character) in source.text().char_indices() {
        if character == '#' && !quoted {
            return Some(SourceSpan::new(
                source.name(),
                offset,
                offset + 1,
                line,
                column,
                line,
                column + 1,
            ));
        }
        if character == '"' && !escaped {
            quoted = !quoted;
        }
        escaped = quoted && character == '\\' && !escaped;
        if character != '\\' {
            escaped = false;
        }
        if character == '\n' {
            line += 1;
            column = 1;
            quoted = false;
            escaped = false;
        } else {
            column += 1;
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn formats_every_profile_to_equivalent_source() {
        let cases = [
            (
                SyntaxProfile::Guided,
                "has x=1\naction add(a,b)\n return a+b\nend\nprint(add(x,2))\n",
                "has x = 1\naction add(a, b)\n    return a + b\nend\nprint(add(x, 2))\n",
            ),
            (
                SyntaxProfile::Structured,
                "int x=1;\nint add(int a,int b){return a+b;}\nprint(add(x,2));\n",
                "int x = 1;\nauto add(auto a, auto b) {\n    return a + b;\n}\nprint(add(x, 2));\n",
            ),
            (
                SyntaxProfile::Advanced,
                "x:=1\nadd(a,b)=>\n  return a+b\nprint(add(x,2))\n",
                "x := 1\nadd(a, b) =>\n    return a + b\nprint(add(x, 2))\n",
            ),
        ];
        for (profile, input, expected) in cases {
            let result = format_profile(&SourceFile::new("test.spr", input), profile);
            assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
            let formatted = result.formatted_source.unwrap();
            assert_eq!(formatted, expected);
            let parsed = parse_profile(&SourceFile::new("test.spr", formatted), profile);
            assert!(parsed.diagnostics.is_empty(), "{:?}", parsed.diagnostics);
        }
    }

    #[test]
    fn refuses_to_discard_comments() {
        let result = format_profile(
            &SourceFile::new("test.spr", "has value = \"# safe\" # keep me\n"),
            SyntaxProfile::Guided,
        );
        assert!(result.formatted_source.is_none());
        assert_eq!(result.diagnostics[0].code, "SPT400");
        assert_eq!(result.diagnostics[0].span.start_column, 22);
    }
}
