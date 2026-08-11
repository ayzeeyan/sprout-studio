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

#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub enum SyntaxProfile {
    #[default]
    Guided,
    Structured,
    Advanced,
}

/// Parses Guided syntax into the canonical Sprout AST.
pub fn parse(source: &SourceFile) -> ParseResult {
    parse_profile(source, SyntaxProfile::Guided)
}

/// Parses one syntax profile into the shared canonical AST.
pub fn parse_profile(source: &SourceFile, profile: SyntaxProfile) -> ParseResult {
    let lexed = lex(source);
    if !lexed.diagnostics.is_empty() {
        return ParseResult {
            program: None,
            diagnostics: lexed.diagnostics,
        };
    }
    let tokens = if profile == SyntaxProfile::Advanced {
        match add_indentation(lexed.tokens) {
            Ok(tokens) => tokens,
            Err(diagnostic) => {
                return ParseResult {
                    program: None,
                    diagnostics: vec![diagnostic],
                };
            }
        }
    } else {
        lexed.tokens
    };
    Parser::new(tokens, profile).parse()
}

struct Parser {
    tokens: Vec<Token>,
    current: usize,
    diagnostics: Vec<Diagnostic>,
    profile: SyntaxProfile,
}

impl Parser {
    fn new(tokens: Vec<Token>, profile: SyntaxProfile) -> Self {
        Self {
            tokens,
            current: 0,
            diagnostics: Vec::new(),
            profile,
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
        match self.profile {
            SyntaxProfile::Guided => self.guided_statement(),
            SyntaxProfile::Structured => self.structured_statement(),
            SyntaxProfile::Advanced => self.advanced_statement(),
        }
    }

    fn guided_statement(&mut self) -> Result<Statement, Diagnostic> {
        if self.take(&TokenKind::Has).is_some() {
            self.variable_statement()
        } else if self.take(&TokenKind::Action).is_some() {
            self.function_statement()
        } else if self.take(&TokenKind::Class).is_some() {
            self.class_statement()
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

    fn structured_statement(&mut self) -> Result<Statement, Diagnostic> {
        if self.take(&TokenKind::Class).is_some() {
            return self.structured_class_statement();
        }
        if self.take(&TokenKind::If).is_some() {
            return self.structured_if_statement();
        }
        if self.take(&TokenKind::Repeat).is_some() {
            return self.structured_repeat_statement();
        }
        if self.take(&TokenKind::While).is_some() {
            return self.structured_while_statement();
        }
        if self.take(&TokenKind::Return).is_some() {
            return self.structured_return_statement();
        }
        if self.is_structured_declaration() {
            return self.structured_declaration();
        }
        if self.is_assignment() {
            return self.structured_assignment();
        }
        let expression = self.expression()?;
        self.finish_structured_statement()?;
        Ok(Node::new(
            StatementKind::Expression(expression.clone()),
            expression.span,
        ))
    }

    fn structured_declaration(&mut self) -> Result<Statement, Diagnostic> {
        let type_token = self.advance().clone();
        let (name, _) = self.expect_identifier("Expected a name after the type.")?;
        if self.take(&TokenKind::LeftParen).is_some() {
            self.structured_function_after_name(type_token.span, name)
        } else {
            self.expect(&TokenKind::Equal, "Expected `=` after the variable name.")?;
            let initializer = self.expression()?;
            self.finish_structured_statement()?;
            let span = type_token.span.merge(&initializer.span);
            Ok(Node::new(
                StatementKind::Variable { name, initializer },
                span,
            ))
        }
    }

    fn structured_function_after_name(
        &mut self,
        start: SourceSpan,
        name: String,
    ) -> Result<Statement, Diagnostic> {
        let mut parameters = Vec::new();
        if !self.check(&TokenKind::RightParen) {
            loop {
                self.expect_identifier("Expected a parameter type.")?;
                parameters.push(self.expect_identifier("Expected a parameter name.")?.0);
                if self.take(&TokenKind::Comma).is_none() {
                    break;
                }
            }
        }
        self.expect(&TokenKind::RightParen, "Expected `)` after parameters.")?;
        self.skip_newlines();
        self.expect(
            &TokenKind::LeftBrace,
            "Expected `{` before the action body.",
        )?;
        self.skip_newlines();
        let body = self.block_until(&[TokenKind::RightBrace]);
        let end = self.expect(
            &TokenKind::RightBrace,
            "Expected `}` after the action body.",
        )?;
        self.skip_newlines();
        Ok(Node::new(
            StatementKind::Function {
                name,
                parameters,
                body,
            },
            start.merge(&end.span),
        ))
    }

    fn structured_class_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let (name, _) = self.expect_identifier("Expected a class name.")?;
        self.skip_newlines();
        self.expect(&TokenKind::LeftBrace, "Expected `{` before the class body.")?;
        self.skip_newlines();
        let mut methods = Vec::new();
        while !self.at_end() && !self.check(&TokenKind::RightBrace) {
            if !self.is_structured_declaration() {
                return Err(self.expected("Expected a typed action declaration in the class."));
            }
            let type_token = self.advance().clone();
            let (method_name, _) = self.expect_identifier("Expected an action name.")?;
            self.expect(&TokenKind::LeftParen, "Expected `(` after the action name.")?;
            methods.push(self.structured_function_after_name(type_token.span, method_name)?);
            self.skip_newlines();
        }
        let end = self.expect(&TokenKind::RightBrace, "Expected `}` after the class body.")?;
        self.skip_newlines();
        Ok(Node::new(
            StatementKind::Class { name, methods },
            start.merge(&end.span),
        ))
    }

    fn structured_if_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        self.expect(&TokenKind::LeftParen, "Expected `(` after `if`.")?;
        let condition = self.expression()?;
        self.expect(&TokenKind::RightParen, "Expected `)` after the condition.")?;
        self.skip_newlines();
        self.expect(&TokenKind::LeftBrace, "Expected `{` before the `if` body.")?;
        self.skip_newlines();
        let then_branch = self.block_until(&[TokenKind::RightBrace]);
        let mut end = self.expect(&TokenKind::RightBrace, "Expected `}` after the `if` body.")?;
        self.skip_newlines();
        let else_branch = if self.take(&TokenKind::Else).is_some() {
            self.skip_newlines();
            self.expect(&TokenKind::LeftBrace, "Expected `{` after `else`.")?;
            self.skip_newlines();
            let branch = self.block_until(&[TokenKind::RightBrace]);
            end = self.expect(
                &TokenKind::RightBrace,
                "Expected `}` after the `else` body.",
            )?;
            self.skip_newlines();
            branch
        } else {
            Vec::new()
        };
        Ok(Node::new(
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            },
            start.merge(&end.span),
        ))
    }

    fn structured_repeat_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        self.expect(&TokenKind::LeftParen, "Expected `(` after `repeat`.")?;
        let count = self.expression()?;
        self.expect(
            &TokenKind::RightParen,
            "Expected `)` after the repeat count.",
        )?;
        self.skip_newlines();
        self.expect(
            &TokenKind::LeftBrace,
            "Expected `{` before the repeat body.",
        )?;
        self.skip_newlines();
        let body = self.block_until(&[TokenKind::RightBrace]);
        let end = self.expect(
            &TokenKind::RightBrace,
            "Expected `}` after the repeat body.",
        )?;
        self.skip_newlines();
        Ok(Node::new(
            StatementKind::Repeat { count, body },
            start.merge(&end.span),
        ))
    }

    fn structured_while_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        self.expect(&TokenKind::LeftParen, "Expected `(` after `while`.")?;
        let condition = self.expression()?;
        self.expect(&TokenKind::RightParen, "Expected `)` after the condition.")?;
        self.skip_newlines();
        self.expect(&TokenKind::LeftBrace, "Expected `{` before the while body.")?;
        self.skip_newlines();
        let body = self.block_until(&[TokenKind::RightBrace]);
        let end = self.expect(&TokenKind::RightBrace, "Expected `}` after the while body.")?;
        self.skip_newlines();
        Ok(Node::new(
            StatementKind::While { condition, body },
            start.merge(&end.span),
        ))
    }

    fn structured_return_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let value = if self.check(&TokenKind::Semicolon) {
            None
        } else {
            Some(self.expression()?)
        };
        let end = value
            .as_ref()
            .map_or_else(|| start.clone(), |expression| expression.span.clone());
        self.finish_structured_statement()?;
        Ok(Node::new(StatementKind::Return(value), start.merge(&end)))
    }

    fn structured_assignment(&mut self) -> Result<Statement, Diagnostic> {
        let (name, start) = self.expect_identifier("Expected an assignment target.")?;
        self.expect(&TokenKind::Equal, "Expected `=` in assignment.")?;
        let value = self.expression()?;
        self.finish_structured_statement()?;
        let span = start.merge(&value.span);
        Ok(Node::new(StatementKind::Assignment { name, value }, span))
    }

    fn finish_structured_statement(&mut self) -> Result<(), Diagnostic> {
        self.expect(&TokenKind::Semicolon, "Expected `;` after the statement.")?;
        self.skip_newlines();
        Ok(())
    }

    fn is_structured_declaration(&self) -> bool {
        matches!(self.peek().kind, TokenKind::Identifier(_))
            && self
                .tokens
                .get(self.current + 1)
                .is_some_and(|token| matches!(token.kind, TokenKind::Identifier(_)))
    }

    fn advanced_statement(&mut self) -> Result<Statement, Diagnostic> {
        if self.take(&TokenKind::Class).is_some() {
            return self.advanced_class_statement();
        }
        if self.take(&TokenKind::If).is_some() {
            return self.advanced_if_statement();
        }
        if self.take(&TokenKind::Repeat).is_some() {
            return self.advanced_repeat_statement();
        }
        if self.take(&TokenKind::While).is_some() {
            return self.advanced_while_statement();
        }
        if self.take(&TokenKind::Return).is_some() {
            return self.advanced_return_statement();
        }
        if self.is_advanced_function() {
            return self.advanced_function_statement();
        }
        if self.is_advanced_variable() {
            return self.advanced_variable_statement();
        }
        if self.is_advanced_assignment() {
            return self.advanced_assignment_statement();
        }
        let expression = self.expression()?;
        self.finish_advanced_statement()?;
        Ok(Node::new(
            StatementKind::Expression(expression.clone()),
            expression.span,
        ))
    }

    fn advanced_variable_statement(&mut self) -> Result<Statement, Diagnostic> {
        let (name, start) = self.expect_identifier("Expected a variable name.")?;
        self.expect(
            &TokenKind::ColonEqual,
            "Expected `:=` after the variable name.",
        )?;
        let initializer = self.expression()?;
        self.finish_advanced_statement()?;
        let span = start.merge(&initializer.span);
        Ok(Node::new(
            StatementKind::Variable { name, initializer },
            span,
        ))
    }

    fn advanced_assignment_statement(&mut self) -> Result<Statement, Diagnostic> {
        let (name, start) = self.expect_identifier("Expected an assignment target.")?;
        let compound = self.take(&TokenKind::PlusEqual).is_some();
        if !compound {
            self.expect(&TokenKind::Equal, "Expected `=` or `+=` in assignment.")?;
        }
        let right = self.expression()?;
        let value = if compound {
            let left = Node::new(ExpressionKind::Variable(name.clone()), start.clone());
            let span = start.merge(&right.span);
            Node::new(
                ExpressionKind::Binary {
                    left: Box::new(left),
                    operator: BinaryOperator::Add,
                    right: Box::new(right),
                },
                span,
            )
        } else {
            right
        };
        self.finish_advanced_statement()?;
        let span = start.merge(&value.span);
        Ok(Node::new(StatementKind::Assignment { name, value }, span))
    }

    fn advanced_function_statement(&mut self) -> Result<Statement, Diagnostic> {
        let (name, start) = self.expect_identifier("Expected an action name.")?;
        self.expect(&TokenKind::LeftParen, "Expected `(` after the action name.")?;
        let mut parameters = Vec::new();
        if !self.check(&TokenKind::RightParen) {
            loop {
                parameters.push(self.expect_identifier("Expected a parameter name.")?.0);
                if self.take(&TokenKind::Colon).is_some() {
                    self.expect_identifier("Expected a type after `:`.")?;
                }
                if self.take(&TokenKind::Comma).is_none() {
                    break;
                }
            }
        }
        self.expect(&TokenKind::RightParen, "Expected `)` after parameters.")?;
        let (body, end) = self.advanced_block("action")?;
        Ok(Node::new(
            StatementKind::Function {
                name,
                parameters,
                body,
            },
            start.merge(&end),
        ))
    }

    fn advanced_class_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let (name, _) = self.expect_identifier("Expected a class name.")?;
        let (methods, end) = self.advanced_block("class")?;
        if methods
            .iter()
            .any(|method| !matches!(method.kind, StatementKind::Function { .. }))
        {
            return Err(Diagnostic::error(
                "SPT100",
                "Unexpected syntax",
                "Only actions may be declared directly inside a class.",
                start,
            ));
        }
        Ok(Node::new(
            StatementKind::Class { name, methods },
            start.merge(&end),
        ))
    }

    fn advanced_if_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let condition = self.expression()?;
        let (then_branch, mut end) = self.advanced_block("if")?;
        let else_branch = if self.take(&TokenKind::Else).is_some() {
            let (branch, branch_end) = self.advanced_block("else")?;
            end = branch_end;
            branch
        } else {
            Vec::new()
        };
        Ok(Node::new(
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            },
            start.merge(&end),
        ))
    }

    fn advanced_repeat_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let count = self.expression()?;
        let (body, end) = self.advanced_block("repeat")?;
        Ok(Node::new(
            StatementKind::Repeat { count, body },
            start.merge(&end),
        ))
    }

    fn advanced_while_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let condition = self.expression()?;
        let (body, end) = self.advanced_block("while")?;
        Ok(Node::new(
            StatementKind::While { condition, body },
            start.merge(&end),
        ))
    }

    fn advanced_return_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let value = if self.check(&TokenKind::Newline) || self.check(&TokenKind::Dedent) {
            None
        } else {
            Some(self.expression()?)
        };
        let end = value
            .as_ref()
            .map_or_else(|| start.clone(), |expression| expression.span.clone());
        self.finish_advanced_statement()?;
        Ok(Node::new(StatementKind::Return(value), start.merge(&end)))
    }

    fn advanced_block(
        &mut self,
        description: &str,
    ) -> Result<(Vec<Statement>, SourceSpan), Diagnostic> {
        self.expect(
            &TokenKind::Arrow,
            &format!("Expected `=>` before the {description} body."),
        )?;
        self.require_line_break(&format!(
            "Expected a new line before the {description} body."
        ))?;
        self.expect(
            &TokenKind::Indent,
            &format!("Expected an indented {description} body."),
        )?;
        self.skip_newlines();
        let body = self.block_until(&[TokenKind::Dedent]);
        let end = self.expect(
            &TokenKind::Dedent,
            &format!("Expected the end of the indented {description} body."),
        )?;
        self.skip_newlines();
        Ok((body, end.span))
    }

    fn finish_advanced_statement(&mut self) -> Result<(), Diagnostic> {
        if self.check(&TokenKind::Newline)
            || self.check(&TokenKind::Dedent)
            || self.check(&TokenKind::Eof)
        {
            self.skip_newlines();
            Ok(())
        } else {
            Err(self.expected("Expected the statement to end here."))
        }
    }

    fn is_advanced_variable(&self) -> bool {
        matches!(self.peek().kind, TokenKind::Identifier(_))
            && self
                .tokens
                .get(self.current + 1)
                .is_some_and(|token| same_kind(&token.kind, &TokenKind::ColonEqual))
    }

    fn is_advanced_assignment(&self) -> bool {
        matches!(self.peek().kind, TokenKind::Identifier(_))
            && self.tokens.get(self.current + 1).is_some_and(|token| {
                same_kind(&token.kind, &TokenKind::Equal)
                    || same_kind(&token.kind, &TokenKind::PlusEqual)
            })
    }

    fn is_advanced_function(&self) -> bool {
        if !matches!(self.peek().kind, TokenKind::Identifier(_))
            || !self
                .tokens
                .get(self.current + 1)
                .is_some_and(|token| same_kind(&token.kind, &TokenKind::LeftParen))
        {
            return false;
        }
        let mut depth = 0usize;
        for (index, token) in self.tokens.iter().enumerate().skip(self.current + 1) {
            match token.kind {
                TokenKind::LeftParen => depth += 1,
                TokenKind::RightParen => {
                    depth = depth.saturating_sub(1);
                    if depth == 0 {
                        return self
                            .tokens
                            .get(index + 1)
                            .is_some_and(|next| same_kind(&next.kind, &TokenKind::Arrow));
                    }
                }
                _ => {}
            }
        }
        false
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

    fn class_statement(&mut self) -> Result<Statement, Diagnostic> {
        let start = self.previous().span.clone();
        let (name, _) = self.expect_identifier("Expected a class name.")?;
        self.require_line_break("Expected a new line before the class body.")?;
        let mut methods = Vec::new();
        while !self.at_end() && !self.check(&TokenKind::End) {
            self.skip_newlines();
            if self.check(&TokenKind::End) {
                break;
            }
            if self.take(&TokenKind::Action).is_none() {
                return Err(self.expected("Only actions may be declared directly inside a class."));
            }
            methods.push(self.function_statement()?);
            self.skip_newlines();
        }
        let end = self.expect(&TokenKind::End, "Expected `end` after the class body.")?;
        self.finish_simple_statement()?;
        Ok(Node::new(
            StatementKind::Class { name, methods },
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
        loop {
            if self.take(&TokenKind::LeftParen).is_some() {
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
            } else if self.take(&TokenKind::Dot).is_some() {
                let (name, member_span) =
                    self.expect_identifier("Expected a member name after `.`.")?;
                let span = expression.span.merge(&member_span);
                expression = Node::new(
                    ExpressionKind::Member {
                        object: Box::new(expression),
                        name,
                        member_span,
                    },
                    span,
                );
            } else {
                break;
            }
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

fn add_indentation(tokens: Vec<Token>) -> Result<Vec<Token>, Diagnostic> {
    let mut output = Vec::with_capacity(tokens.len());
    let mut indents = vec![0usize];
    let mut current = 0usize;
    let mut delimiter_depth = 0usize;

    while current < tokens.len() {
        if same_kind(&tokens[current].kind, &TokenKind::Newline) {
            while current < tokens.len() && same_kind(&tokens[current].kind, &TokenKind::Newline) {
                output.push(tokens[current].clone());
                current += 1;
            }
            if current >= tokens.len() || delimiter_depth > 0 {
                continue;
            }
            let next = &tokens[current];
            let indentation = if same_kind(&next.kind, &TokenKind::Eof) {
                0
            } else {
                next.span.start_column.saturating_sub(1)
            };
            let active = *indents.last().expect("indentation stack is never empty");
            if indentation > active {
                indents.push(indentation);
                output.push(Token {
                    kind: TokenKind::Indent,
                    span: next.span.clone(),
                });
            } else if indentation < active {
                while indentation < *indents.last().expect("indentation stack is never empty") {
                    indents.pop();
                    output.push(Token {
                        kind: TokenKind::Dedent,
                        span: next.span.clone(),
                    });
                }
                if indentation != *indents.last().expect("indentation stack is never empty") {
                    return Err(Diagnostic::error(
                        "SPT005",
                        "Inconsistent indentation",
                        "This line does not align with an enclosing Advanced block.",
                        next.span.clone(),
                    ));
                }
            }
            continue;
        }

        let token = tokens[current].clone();
        match token.kind {
            TokenKind::LeftParen => delimiter_depth += 1,
            TokenKind::RightParen => delimiter_depth = delimiter_depth.saturating_sub(1),
            TokenKind::Eof => {
                while indents.len() > 1 {
                    indents.pop();
                    output.push(Token {
                        kind: TokenKind::Dedent,
                        span: token.span.clone(),
                    });
                }
            }
            _ => {}
        }
        output.push(token);
        current += 1;
    }
    Ok(output)
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

    #[test]
    fn parses_structured_profile_into_canonical_nodes() {
        let result = parse_profile(
            &SourceFile::new(
                "structured.spr",
                "int score = 0;\nvoid addScore(int amount) {\n  score = score + amount;\n}\nrepeat (3) {\n  addScore(1);\n}\n",
            ),
            SyntaxProfile::Structured,
        );
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        let program = result.program.unwrap();
        assert!(matches!(
            program.statements[0].kind,
            StatementKind::Variable { .. }
        ));
        assert!(matches!(
            program.statements[1].kind,
            StatementKind::Function { .. }
        ));
        assert!(matches!(
            program.statements[2].kind,
            StatementKind::Repeat { .. }
        ));
    }

    #[test]
    fn parses_indentation_aware_advanced_profile() {
        let result = parse_profile(
            &SourceFile::new(
                "advanced.spr",
                "score := 0\naddScore(amount: Int) =>\n    score += amount\nrepeat 3 =>\n    addScore(1)\n",
            ),
            SyntaxProfile::Advanced,
        );
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        let program = result.program.unwrap();
        assert_eq!(program.statements.len(), 3);
        let StatementKind::Function { body, .. } = &program.statements[1].kind else {
            panic!("expected canonical function")
        };
        let StatementKind::Assignment { value, .. } = &body[0].kind else {
            panic!("expected canonical assignment")
        };
        assert!(matches!(value.kind, ExpressionKind::Binary { .. }));
    }

    #[test]
    fn advanced_profile_rejects_inconsistent_dedent() {
        let result = parse_profile(
            &SourceFile::new("bad-advanced.spr", "if true =>\n    print(1)\n  print(2)\n"),
            SyntaxProfile::Advanced,
        );
        assert_eq!(result.diagnostics[0].code, "SPT005");
        assert_eq!(result.diagnostics[0].span.start_line, 3);
    }

    #[test]
    fn all_profiles_produce_semantically_equivalent_canonical_ast() {
        let cases = [
            (
                SyntaxProfile::Guided,
                "has score = 0\naction addScore(amount)\n  score = score + amount\nend\nrepeat 3 times\n  addScore(1)\nend\nprint(score)\n",
            ),
            (
                SyntaxProfile::Structured,
                "int score = 0;\nvoid addScore(int amount) {\n  score = score + amount;\n}\nrepeat (3) {\n  addScore(1);\n}\nprint(score);\n",
            ),
            (
                SyntaxProfile::Advanced,
                "score := 0\naddScore(amount: Int) =>\n  score += amount\nrepeat 3 =>\n  addScore(1)\nprint(score)\n",
            ),
        ];
        let programs = cases.map(|(profile, text)| {
            let result = parse_profile(&SourceFile::new("profile.spr", text), profile);
            assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
            result.program.unwrap()
        });
        assert!(programs[0].semantically_equivalent(&programs[1]));
        assert!(programs[1].semantically_equivalent(&programs[2]));
    }
}
