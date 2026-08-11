use crate::ast::{
    BinaryOperator, Expression, ExpressionKind, Node, Program, Statement, StatementKind,
    UnaryOperator,
};
use crate::diagnostic::Diagnostic;
use crate::lexer::lex;
use crate::source::{SourceFile, SourceSpan};
use crate::token::{Token, TokenKind};
use std::mem::discriminant;

pub struct ParseResult {
    pub program: Option<Program>,
    pub diagnostics: Vec<Diagnostic>,
}

/// Parses Guided syntax into the canonical Sprout AST.
pub fn parse(source: &SourceFile) -> ParseResult {
    let lexed = lex(source);
    if !lexed.diagnostics.is_empty() {
        return ParseResult {
            program: None,
            diagnostics: lexed.diagnostics,
        };
    }
    Parser::new(lexed.tokens).parse()
}

struct Parser {
    tokens: Vec<Token>,
    current: usize,
    diagnostics: Vec<Diagnostic>,
}

impl Parser {
    fn new(tokens: Vec<Token>) -> Self {
        Self {
            tokens,
            current: 0,
            diagnostics: Vec::new(),
        }
    }

    fn parse(mut self) -> ParseResult {
        self.skip_newlines();
        let statements = self.block_until(&[TokenKind::Eof]);
        let program = if self.diagnostics.is_empty() {
            Some(Program { statements })
        } else {
            None
        };
        ParseResult {
            program,
            diagnostics: self.diagnostics,
        }
    }

    fn block_until(&mut self, terminators: &[TokenKind]) -> Vec<Statement> {
        let mut statements = Vec::new();
        while !self.at_end() && !terminators.iter().any(|kind| self.check(kind)) {
            match self.statement() {
                Ok(statement) => statements.push(statement),
                Err(diagnostic) => {
                    self.diagnostics.push(diagnostic);
                    self.synchronize();
                }
            }
            self.skip_newlines();
        }
        statements
    }

    fn statement(&mut self) -> Result<Statement, Diagnostic> {
        if self.take(&TokenKind::Has).is_some() {
            self.variable_statement()
        } else if self.take(&TokenKind::Action).is_some() {
            self.function_statement()
        } else if self.take(&TokenKind::If).is_some() {
            self.if_statement()
        } else if self.take(&TokenKind::Repeat).is_some() {
            self.repeat_statement()
        } else if self.take(&TokenKind::While).is_some() {
            self.while_statement()
        } else if self.take(&TokenKind::Return).is_some() {
            let start = self.previous().span.clone();
            self.return_statement(start)
        } else if self.is_assignment() {
            self.assignment_statement()
        } else {
            let expression = self.expression()?;
            self.finish_simple_statement()?;
            Ok(Node::new(
                StatementKind::Expression(expression.clone()),
                expression.span,
            ))
        }
    }

    fn variable_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let (name, _) = self.expect_identifier("Expected a variable name after `has`.")?;
        self.expect(&TokenKind::Equal, "Expected `=` after the variable name.")?;
        let initializer = self.expression()?;
        self.finish_simple_statement()?;
        let span = start.merge(&initializer.span);
        Ok(Node::new(
            StatementKind::Variable { name, initializer },
            span,
        ))
    }

    fn assignment_statement(&mut self) -> Result<Statement, Diagnostic> {
        let (name, start) = self.expect_identifier("Expected an assignment target.")?;
        self.expect(&TokenKind::Equal, "Expected `=` in assignment.")?;
        let value = self.expression()?;
        self.finish_simple_statement()?;
        let span = start.merge(&value.span);
        Ok(Node::new(StatementKind::Assignment { name, value }, span))
    }

    fn function_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let (name, _) = self.expect_identifier("Expected an action name.")?;
        self.expect(&TokenKind::LeftParen, "Expected `(` after the action name.")?;
        let mut parameters = Vec::new();
        if !self.check(&TokenKind::RightParen) {
            loop {
                parameters.push(self.expect_identifier("Expected a parameter name.")?.0);
                if self.take(&TokenKind::Comma).is_none() {
                    break;
                }
            }
        }
        self.expect(&TokenKind::RightParen, "Expected `)` after parameters.")?;
        self.require_line_break("Expected a new line before the action body.")?;
        let body = self.block_until(&[TokenKind::End]);
        let end = self.expect(&TokenKind::End, "Expected `end` after the action body.")?;
        self.finish_simple_statement()?;
        Ok(Node::new(
            StatementKind::Function {
                name,
                parameters,
                body,
            },
            start.merge(&end.span),
        ))
    }

    fn if_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let condition = self.expression()?;
        self.require_line_break("Expected a new line after the `if` condition.")?;
        let then_branch = self.block_until(&[TokenKind::Else, TokenKind::End]);
        let else_branch = if self.take(&TokenKind::Else).is_some() {
            self.require_line_break("Expected a new line after `else`.")?;
            self.block_until(&[TokenKind::End])
        } else {
            Vec::new()
        };
        let end = self.expect(&TokenKind::End, "Expected `end` after the `if` statement.")?;
        self.finish_simple_statement()?;
        Ok(Node::new(
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            },
            start.merge(&end.span),
        ))
    }

    fn repeat_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let count = self.expression()?;
        self.expect(
            &TokenKind::Times,
            "Expected `times` after the repeat count.",
        )?;
        self.require_line_break("Expected a new line before the repeat body.")?;
        let body = self.block_until(&[TokenKind::End]);
        let end = self.expect(&TokenKind::End, "Expected `end` after the repeat body.")?;
        self.finish_simple_statement()?;
        Ok(Node::new(
            StatementKind::Repeat { count, body },
            start.merge(&end.span),
        ))
    }

    fn while_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let condition = self.expression()?;
        self.require_line_break("Expected a new line before the while body.")?;
        let body = self.block_until(&[TokenKind::End]);
        let end = self.expect(&TokenKind::End, "Expected `end` after the while body.")?;
        self.finish_simple_statement()?;
        Ok(Node::new(
            StatementKind::While { condition, body },
            start.merge(&end.span),
        ))
    }

    fn return_statement(&mut self, start: SourceSpan) -> Result<Statement, Diagnostic> {
        let value = if self.check(&TokenKind::Newline) || self.check(&TokenKind::Eof) {
            None
        } else {
            Some(self.expression()?)
        };
        let end = value
            .as_ref()
            .map_or_else(|| start.clone(), |expression| expression.span.clone());
        self.finish_simple_statement()?;
        Ok(Node::new(StatementKind::Return(value), start.merge(&end)))
    }

    fn expression(&mut self) -> Result<Expression, Diagnostic> {
        self.binary(1)
    }

    fn binary(&mut self, minimum_precedence: u8) -> Result<Expression, Diagnostic> {
        let mut left = self.unary()?;
        while let Some((operator, precedence)) = self.binary_operator() {
            if precedence < minimum_precedence {
                break;
            }
            self.advance();
            let right = self.binary(precedence + 1)?;
            let span = left.span.merge(&right.span);
            left = Node::new(
                ExpressionKind::Binary {
                    left: Box::new(left),
                    operator,
                    right: Box::new(right),
                },
                span,
            );
        }
        Ok(left)
    }

    fn unary(&mut self) -> Result<Expression, Diagnostic> {
        let operator = if self.take(&TokenKind::Minus).is_some() {
            Some(UnaryOperator::Negate)
        } else if self.take(&TokenKind::Not).is_some() {
            Some(UnaryOperator::Not)
        } else {
            None
        };
        if let Some(operator) = operator {
            let start = self.previous().span.clone();
            let operand = self.unary()?;
            let span = start.merge(&operand.span);
            return Ok(Node::new(
                ExpressionKind::Unary {
                    operator,
                    operand: Box::new(operand),
                },
                span,
            ));
        }
        self.call()
    }

    fn call(&mut self) -> Result<Expression, Diagnostic> {
        let mut expression = self.primary()?;
        while self.take(&TokenKind::LeftParen).is_some() {
            let mut arguments = Vec::new();
            if !self.check(&TokenKind::RightParen) {
                loop {
                    arguments.push(self.expression()?);
                    if self.take(&TokenKind::Comma).is_none() {
                        break;
                    }
                }
            }
            let close = self.expect(&TokenKind::RightParen, "Expected `)` after arguments.")?;
            let span = expression.span.merge(&close.span);
            expression = Node::new(
                ExpressionKind::Call {
                    callee: Box::new(expression),
                    arguments,
                },
                span,
            );
        }
        Ok(expression)
    }

    fn primary(&mut self) -> Result<Expression, Diagnostic> {
        let token = self.advance().clone();
        let kind = match token.kind {
            TokenKind::Integer(value) => ExpressionKind::Integer(value),
            TokenKind::Float(value) => ExpressionKind::Float(value),
            TokenKind::String(value) => ExpressionKind::String(value),
            TokenKind::True => ExpressionKind::Bool(true),
            TokenKind::False => ExpressionKind::Bool(false),
            TokenKind::Nil => ExpressionKind::Nil,
            TokenKind::Identifier(name) => ExpressionKind::Variable(name),
            TokenKind::LeftParen => {
                let expression = self.expression()?;
                self.expect(&TokenKind::RightParen, "Expected `)` after the expression.")?;
                return Ok(expression);
            }
            _ => {
                return Err(Diagnostic::error(
                    "SPT101",
                    "Expected an expression",
                    format!("Found {} instead.", token.kind.name()),
                    token.span,
                ));
            }
        };
        Ok(Node::new(kind, token.span))
    }

    fn binary_operator(&self) -> Option<(BinaryOperator, u8)> {
        Some(match self.peek().kind {
            TokenKind::Or => (BinaryOperator::Or, 1),
            TokenKind::And => (BinaryOperator::And, 2),
            TokenKind::EqualEqual => (BinaryOperator::Equal, 3),
            TokenKind::BangEqual => (BinaryOperator::NotEqual, 3),
            TokenKind::Less => (BinaryOperator::Less, 4),
            TokenKind::LessEqual => (BinaryOperator::LessEqual, 4),
            TokenKind::Greater => (BinaryOperator::Greater, 4),
            TokenKind::GreaterEqual => (BinaryOperator::GreaterEqual, 4),
            TokenKind::Plus => (BinaryOperator::Add, 5),
            TokenKind::Minus => (BinaryOperator::Subtract, 5),
            TokenKind::Star => (BinaryOperator::Multiply, 6),
            TokenKind::Slash => (BinaryOperator::Divide, 6),
            TokenKind::Percent => (BinaryOperator::Remainder, 6),
            _ => return None,
        })
    }

    fn is_assignment(&self) -> bool {
        matches!(self.peek().kind, TokenKind::Identifier(_))
            && self
                .tokens
                .get(self.current + 1)
                .is_some_and(|token| same_kind(&token.kind, &TokenKind::Equal))
    }

    fn require_line_break(&mut self, message: &str) -> Result<(), Diagnostic> {
        if self.take(&TokenKind::Newline).is_none() {
            return Err(self.expected(message));
        }
        self.skip_newlines();
        Ok(())
    }

    fn finish_simple_statement(&mut self) -> Result<(), Diagnostic> {
        if self.check(&TokenKind::Newline) || self.check(&TokenKind::Eof) {
            self.skip_newlines();
            Ok(())
        } else {
            Err(self.expected("Expected the statement to end here."))
        }
    }

    fn expect_identifier(&mut self, message: &str) -> Result<(String, SourceSpan), Diagnostic> {
        let token = self.advance().clone();
        match token.kind {
            TokenKind::Identifier(name) => Ok((name, token.span)),
            _ => Err(Diagnostic::error(
                "SPT102",
                "Expected a name",
                message,
                token.span,
            )),
        }
    }

    fn expect(&mut self, kind: &TokenKind, message: &str) -> Result<Token, Diagnostic> {
        self.take(kind)
            .cloned()
            .ok_or_else(|| self.expected(message))
    }

    fn expected(&self, message: &str) -> Diagnostic {
        Diagnostic::error(
            "SPT100",
            "Unexpected syntax",
            message,
            self.peek().span.clone(),
        )
    }

    fn synchronize(&mut self) {
        while !self.at_end() && !self.check(&TokenKind::Newline) {
            self.advance();
        }
        self.skip_newlines();
    }

    fn skip_newlines(&mut self) {
        while self.take(&TokenKind::Newline).is_some() {}
    }

    fn take(&mut self, kind: &TokenKind) -> Option<&Token> {
        if self.check(kind) {
            self.current += 1;
            Some(self.previous())
        } else {
            None
        }
    }

    fn check(&self, kind: &TokenKind) -> bool {
        same_kind(&self.peek().kind, kind)
    }

    fn advance(&mut self) -> &Token {
        let consumed = self.current;
        if !self.at_end() {
            self.current += 1;
        }
        &self.tokens[consumed]
    }

    fn at_end(&self) -> bool {
        self.check(&TokenKind::Eof)
    }

    fn peek(&self) -> &Token {
        &self.tokens[self.current]
    }

    fn previous(&self) -> &Token {
        &self.tokens[self.current.saturating_sub(1)]
    }
}

fn same_kind(left: &TokenKind, right: &TokenKind) -> bool {
    discriminant(left) == discriminant(right)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn parsed(text: &str) -> Program {
        let result = parse(&SourceFile::new("test.spr", text));
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        result.program.unwrap()
    }

    #[test]
    fn parses_functions_loops_and_calls() {
        let program = parsed(
            "action greet(person)\n  print(\"Hello \" + person)\nend\nrepeat 3 times\n  greet(\"Sprout\")\nend\n",
        );
        assert_eq!(program.statements.len(), 2);
        assert!(matches!(
            program.statements[0].kind,
            StatementKind::Function { .. }
        ));
        assert!(matches!(
            program.statements[1].kind,
            StatementKind::Repeat { .. }
        ));
    }

    #[test]
    fn observes_arithmetic_precedence() {
        let program = parsed("has value = 1 + 2 * 3\n");
        let StatementKind::Variable { initializer, .. } = &program.statements[0].kind else {
            panic!("expected variable declaration")
        };
        let ExpressionKind::Binary { right, .. } = &initializer.kind else {
            panic!("expected addition")
        };
        assert!(matches!(right.kind, ExpressionKind::Binary { .. }));
    }

    #[test]
    fn reports_missing_block_end_without_panicking() {
        let result = parse(&SourceFile::new("bad.spr", "if true\nprint(1)\n"));
        assert!(result.program.is_none());
        assert_eq!(result.diagnostics[0].code, "SPT100");
        assert_eq!(result.diagnostics[0].span.start_line, 3);
    }
}
