use crate::diagnostic::Diagnostic;
use crate::source::{SourceFile, SourceSpan};
use crate::token::{Token, TokenKind};

pub struct LexResult {
    pub tokens: Vec<Token>,
    pub diagnostics: Vec<Diagnostic>,
}

pub fn lex(source: &SourceFile) -> LexResult {
    Lexer::new(source).scan()
}

struct Lexer<'a> {
    source: &'a SourceFile,
    offset: usize,
    line: usize,
    column: usize,
    tokens: Vec<Token>,
    diagnostics: Vec<Diagnostic>,
}

impl<'a> Lexer<'a> {
    fn new(source: &'a SourceFile) -> Self {
        Self {
            source,
            offset: 0,
            line: 1,
            column: 1,
            tokens: Vec::new(),
            diagnostics: Vec::new(),
        }
    }

    fn scan(mut self) -> LexResult {
        while let Some(character) = self.peek() {
            let start = self.mark();
            match character {
                ' ' | '\t' | '\r' => {
                    self.advance();
                }
                '\n' => {
                    self.advance();
                    self.push(TokenKind::Newline, start);
                }
                '#' => self.skip_comment(),
                '0'..='9' => self.number(start),
                'a'..='z' | 'A'..='Z' | '_' => self.identifier(start),
                '"' => self.string(start),
                '(' => self.single(TokenKind::LeftParen, start),
                ')' => self.single(TokenKind::RightParen, start),
                ',' => self.single(TokenKind::Comma, start),
                '+' => self.single(TokenKind::Plus, start),
                '-' => self.single(TokenKind::Minus, start),
                '*' => self.single(TokenKind::Star, start),
                '/' => self.single(TokenKind::Slash, start),
                '%' => self.single(TokenKind::Percent, start),
                '=' => self.one_or_two(TokenKind::Equal, TokenKind::EqualEqual, start),
                '!' => {
                    self.advance();
                    if self.take('=') {
                        self.push(TokenKind::BangEqual, start);
                    } else {
                        self.error(
                            "SPT003",
                            "Unexpected character",
                            "`!` must be followed by `=`.",
                            start,
                        );
                    }
                }
                '<' => self.one_or_two(TokenKind::Less, TokenKind::LessEqual, start),
                '>' => self.one_or_two(TokenKind::Greater, TokenKind::GreaterEqual, start),
                _ => {
                    self.advance();
                    self.error(
                        "SPT001",
                        "Unexpected character",
                        format!("Sprout does not recognize `{character}` here."),
                        start,
                    );
                }
            }
        }
        let mark = self.mark();
        self.push(TokenKind::Eof, mark);
        LexResult {
            tokens: self.tokens,
            diagnostics: self.diagnostics,
        }
    }

    fn identifier(&mut self, start: Mark) {
        self.advance();
        while self
            .peek()
            .is_some_and(|c| c.is_ascii_alphanumeric() || c == '_')
        {
            self.advance();
        }
        let text = &self.source.text()[start.offset..self.offset];
        let kind = match text {
            "has" => TokenKind::Has,
            "action" => TokenKind::Action,
            "end" => TokenKind::End,
            "if" => TokenKind::If,
            "else" => TokenKind::Else,
            "repeat" => TokenKind::Repeat,
            "times" => TokenKind::Times,
            "while" => TokenKind::While,
            "return" => TokenKind::Return,
            "true" => TokenKind::True,
            "false" => TokenKind::False,
            "nil" => TokenKind::Nil,
            "and" => TokenKind::And,
            "or" => TokenKind::Or,
            "not" => TokenKind::Not,
            _ => TokenKind::Identifier(text.to_owned()),
        };
        self.push(kind, start);
    }

    fn number(&mut self, start: Mark) {
        self.advance();
        while self.peek().is_some_and(|c| c.is_ascii_digit()) {
            self.advance();
        }
        let is_float =
            self.peek() == Some('.') && self.peek_next().is_some_and(|c| c.is_ascii_digit());
        if is_float {
            self.advance();
            while self.peek().is_some_and(|c| c.is_ascii_digit()) {
                self.advance();
            }
        }
        let text = &self.source.text()[start.offset..self.offset];
        let kind = if is_float {
            TokenKind::Float(text.parse().expect("lexer accepted only float digits"))
        } else {
            match text.parse() {
                Ok(value) => TokenKind::Integer(value),
                Err(_) => {
                    self.error(
                        "SPT002",
                        "Number is too large",
                        "This integer does not fit in a Sprout Int.",
                        start,
                    );
                    return;
                }
            }
        };
        self.push(kind, start);
    }

    fn string(&mut self, start: Mark) {
        self.advance();
        let mut value = String::new();
        while let Some(character) = self.peek() {
            if character == '"' {
                self.advance();
                self.push(TokenKind::String(value), start);
                return;
            }
            if character == '\n' {
                break;
            }
            self.advance();
            if character == '\\' {
                match self.peek() {
                    Some('n') => {
                        self.advance();
                        value.push('\n');
                    }
                    Some('t') => {
                        self.advance();
                        value.push('\t');
                    }
                    Some('"') => {
                        self.advance();
                        value.push('"');
                    }
                    Some('\\') => {
                        self.advance();
                        value.push('\\');
                    }
                    _ => value.push('\\'),
                }
            } else {
                value.push(character);
            }
        }
        self.error(
            "SPT004",
            "Unterminated string",
            "Add a closing double quote before the end of the line.",
            start,
        );
    }

    fn skip_comment(&mut self) {
        while self.peek().is_some_and(|c| c != '\n') {
            self.advance();
        }
    }

    fn single(&mut self, kind: TokenKind, start: Mark) {
        self.advance();
        self.push(kind, start);
    }

    fn one_or_two(&mut self, one: TokenKind, two: TokenKind, start: Mark) {
        self.advance();
        let kind = if self.take('=') { two } else { one };
        self.push(kind, start);
    }

    fn take(&mut self, expected: char) -> bool {
        if self.peek() == Some(expected) {
            self.advance();
            true
        } else {
            false
        }
    }

    fn peek(&self) -> Option<char> {
        self.source.text()[self.offset..].chars().next()
    }

    fn peek_next(&self) -> Option<char> {
        let mut chars = self.source.text()[self.offset..].chars();
        chars.next()?;
        chars.next()
    }

    fn advance(&mut self) -> Option<char> {
        let character = self.peek()?;
        self.offset += character.len_utf8();
        if character == '\n' {
            self.line += 1;
            self.column = 1;
        } else {
            self.column += 1;
        }
        Some(character)
    }

    fn mark(&self) -> Mark {
        Mark {
            offset: self.offset,
            line: self.line,
            column: self.column,
        }
    }

    fn span(&self, start: Mark) -> SourceSpan {
        SourceSpan::new(
            self.source.name(),
            start.offset,
            self.offset,
            start.line,
            start.column,
            self.line,
            self.column,
        )
    }

    fn push(&mut self, kind: TokenKind, start: Mark) {
        self.tokens.push(Token {
            kind,
            span: self.span(start),
        });
    }

    fn error(&mut self, code: &'static str, title: &str, message: impl Into<String>, start: Mark) {
        self.diagnostics
            .push(Diagnostic::error(code, title, message, self.span(start)));
    }
}

#[derive(Clone, Copy)]
struct Mark {
    offset: usize,
    line: usize,
    column: usize,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tokenizes_guided_program_with_locations() {
        let source = SourceFile::new("test.spr", "has score = 3\nprint(score)\n");
        let result = lex(&source);
        assert!(result.diagnostics.is_empty());
        assert!(matches!(result.tokens[0].kind, TokenKind::Has));
        assert_eq!(result.tokens[0].span.start_line, 1);
        assert_eq!(result.tokens[5].span.start_line, 2);
        assert_eq!(result.tokens[5].span.start_column, 1);
    }

    #[test]
    fn reports_invalid_character_at_exact_span() {
        let source = SourceFile::new("bad.spr", "has x = @\n");
        let result = lex(&source);
        assert_eq!(result.diagnostics[0].code, "SPT001");
        assert_eq!(result.diagnostics[0].span.start_column, 9);
    }

    #[test]
    fn decodes_string_escapes() {
        let source = SourceFile::new("test.spr", "\"hello\\nworld\"");
        let result = lex(&source);
        assert_eq!(
            result.tokens[0].kind,
            TokenKind::String("hello\nworld".into())
        );
    }
}
