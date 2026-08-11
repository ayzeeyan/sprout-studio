//! UI-independent language-service queries over canonical Sprout syntax.

use crate::ast::{Expression, ExpressionKind, Statement, StatementKind};
use crate::diagnostic::Diagnostic;
use crate::lexer::lex;
use crate::parser::{SyntaxProfile, parse_profile};
use crate::source::{SourceFile, SourceSpan};
use crate::token::{Token, TokenKind};
use std::collections::HashSet;

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct CompletionItem {
    pub label: String,
    pub kind: String,
    pub detail: String,
    pub insert_text: String,
    pub replacement_span: SourceSpan,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct HoverInfo {
    pub title: String,
    pub contents: String,
    pub span: SourceSpan,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DefinitionLocation {
    pub name: String,
    pub kind: String,
    pub span: SourceSpan,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DocumentSymbol {
    pub name: String,
    pub kind: String,
    pub detail: String,
    pub span: SourceSpan,
    pub children: Vec<DocumentSymbol>,
}

pub struct InspectResult {
    pub diagnostics: Vec<Diagnostic>,
    pub completions: Vec<CompletionItem>,
    pub hover: Option<HoverInfo>,
    pub definition: Option<DefinitionLocation>,
    pub symbols: Vec<DocumentSymbol>,
}

pub fn inspect(source: &SourceFile, profile: SyntaxProfile, cursor: usize) -> InspectResult {
    let cursor = cursor.min(source.text().len());
    let replacement_span = word_span(source, cursor);
    let prefix = source
        .text()
        .get(replacement_span.start..cursor)
        .unwrap_or_default();
    let parsed = parse_profile(source, profile);
    let mut entries = builtin_entries(source);
    let mut symbols = Vec::new();
    if let Some(program) = &parsed.program {
        collect_statements(
            source,
            &program.statements,
            0,
            source.text().len(),
            &mut entries,
            &mut symbols,
        );
    } else {
        collect_lexical_declarations(source, profile, &mut entries);
    }

    let mut completions = Vec::new();
    let mut labels = HashSet::new();
    for (label, detail) in keyword_items(profile) {
        add_completion(
            &mut completions,
            &mut labels,
            prefix,
            label,
            "keyword",
            detail,
            label,
            &replacement_span,
        );
    }
    for entry in entries.iter().filter(|entry| entry.visible_at(cursor)) {
        add_completion(
            &mut completions,
            &mut labels,
            prefix,
            &entry.name,
            &entry.kind,
            &entry.detail,
            &entry.insert_text,
            &replacement_span,
        );
    }
    completions.sort_by(|left, right| {
        left.kind
            .cmp(&right.kind)
            .then_with(|| left.label.to_lowercase().cmp(&right.label.to_lowercase()))
    });

    let selected_word = source
        .text()
        .get(replacement_span.start..replacement_span.end)
        .unwrap_or_default();
    let resolved = entries
        .iter()
        .filter(|entry| entry.name == selected_word && entry.visible_at(cursor))
        .max_by_key(|entry| entry.available_start);
    let hover = resolved.map(|entry| HoverInfo {
        title: entry.name.clone(),
        contents: entry.detail.clone(),
        span: replacement_span.clone(),
    });
    let definition = resolved.and_then(|entry| {
        entry
            .declaration_span
            .as_ref()
            .map(|span| DefinitionLocation {
                name: entry.name.clone(),
                kind: entry.kind.clone(),
                span: span.clone(),
            })
    });

    InspectResult {
        diagnostics: parsed.diagnostics,
        completions,
        hover,
        definition,
        symbols,
    }
}

#[derive(Clone)]
struct SymbolEntry {
    name: String,
    kind: String,
    detail: String,
    insert_text: String,
    declaration_span: Option<SourceSpan>,
    available_start: usize,
    available_end: usize,
}

impl SymbolEntry {
    fn visible_at(&self, cursor: usize) -> bool {
        self.available_start <= cursor && cursor <= self.available_end
    }
}

fn builtin_entries(source: &SourceFile) -> Vec<SymbolEntry> {
    [
        ("print", "action print(values…) -> Nil"),
        ("canvas_clear", "action canvas_clear(color: String) -> Nil"),
        (
            "canvas_rect",
            "action canvas_rect(x, y, width, height, color: String) -> Nil",
        ),
        (
            "canvas_circle",
            "action canvas_circle(x, y, radius, color: String) -> Nil",
        ),
    ]
    .into_iter()
    .map(|(name, detail)| SymbolEntry {
        name: name.to_owned(),
        kind: "action".to_owned(),
        detail: detail.to_owned(),
        insert_text: name.to_owned(),
        declaration_span: None,
        available_start: 0,
        available_end: source.text().len(),
    })
    .collect()
}

fn collect_statements(
    source: &SourceFile,
    statements: &[Statement],
    scope_start: usize,
    scope_end: usize,
    entries: &mut Vec<SymbolEntry>,
    symbols: &mut Vec<DocumentSymbol>,
) {
    for statement in statements {
        match &statement.kind {
            StatementKind::ImportPython { module, alias } => {
                let span = identifier_span(source, &statement.span, alias, 0);
                entries.push(entry(
                    alias,
                    "module",
                    &format!("Python module alias for `{module}` (dynamic value)"),
                    span.clone(),
                    statement.span.start,
                    scope_end,
                ));
                symbols.push(symbol(alias, "module", module, span, Vec::new()));
            }
            StatementKind::Variable { name, initializer } => {
                let span = identifier_span(source, &statement.span, name, 0);
                let detail = format!("variable {name}: {}", inferred_type(initializer));
                entries.push(entry(
                    name,
                    "variable",
                    &detail,
                    span.clone(),
                    statement.span.start,
                    scope_end,
                ));
                symbols.push(symbol(name, "variable", &detail, span, Vec::new()));
            }
            StatementKind::Function {
                name,
                parameters,
                body,
            } => {
                let name_span = identifier_span(source, &statement.span, name, 0);
                let detail = format!("action {name}({})", parameters.join(", "));
                entries.push(entry(
                    name,
                    "action",
                    &detail,
                    name_span.clone(),
                    statement.span.start,
                    scope_end,
                ));
                let mut children = Vec::new();
                let mut search_start = name_span.end;
                for parameter in parameters {
                    let span = identifier_span(source, &statement.span, parameter, search_start);
                    search_start = span.end;
                    let parameter_detail = format!("parameter {parameter}");
                    entries.push(entry(
                        parameter,
                        "parameter",
                        &parameter_detail,
                        span.clone(),
                        statement.span.start,
                        statement.span.end,
                    ));
                    children.push(symbol(
                        parameter,
                        "parameter",
                        &parameter_detail,
                        span,
                        Vec::new(),
                    ));
                }
                collect_statements(
                    source,
                    body,
                    statement.span.start,
                    statement.span.end,
                    entries,
                    &mut children,
                );
                symbols.push(symbol(name, "action", &detail, name_span, children));
            }
            StatementKind::Class { name, methods } => {
                let name_span = identifier_span(source, &statement.span, name, 0);
                let detail = format!("class {name}");
                entries.push(entry(
                    name,
                    "class",
                    &detail,
                    name_span.clone(),
                    statement.span.start,
                    scope_end,
                ));
                let mut children = Vec::new();
                collect_statements(
                    source,
                    methods,
                    statement.span.start,
                    statement.span.end,
                    entries,
                    &mut children,
                );
                symbols.push(symbol(name, "class", &detail, name_span, children));
            }
            StatementKind::If {
                then_branch,
                else_branch,
                ..
            } => {
                collect_statements(
                    source,
                    then_branch,
                    statement.span.start.max(scope_start),
                    statement.span.end,
                    entries,
                    symbols,
                );
                collect_statements(
                    source,
                    else_branch,
                    statement.span.start.max(scope_start),
                    statement.span.end,
                    entries,
                    symbols,
                );
            }
            StatementKind::Repeat { body, .. } | StatementKind::While { body, .. } => {
                collect_statements(
                    source,
                    body,
                    statement.span.start.max(scope_start),
                    statement.span.end,
                    entries,
                    symbols,
                );
            }
            StatementKind::Assignment { .. }
            | StatementKind::Expression(_)
            | StatementKind::Return(_) => {}
        }
    }
}

fn collect_lexical_declarations(
    source: &SourceFile,
    profile: SyntaxProfile,
    entries: &mut Vec<SymbolEntry>,
) {
    let lexed = lex(source);
    let tokens = lexed.tokens;
    for (index, token) in tokens.iter().enumerate() {
        let candidate = match (&token.kind, tokens.get(index + 1), tokens.get(index + 2)) {
            (
                TokenKind::Has | TokenKind::Action | TokenKind::Class | TokenKind::As,
                Some(Token {
                    kind: TokenKind::Identifier(name),
                    span,
                }),
                _,
            ) => Some((name, span, "symbol")),
            (
                TokenKind::Identifier(name),
                Some(Token {
                    kind: TokenKind::ColonEqual,
                    ..
                }),
                _,
            ) if profile == SyntaxProfile::Advanced => Some((name, &token.span, "variable")),
            (
                TokenKind::Identifier(_),
                Some(Token {
                    kind: TokenKind::Identifier(name),
                    span,
                }),
                Some(Token {
                    kind: TokenKind::Equal | TokenKind::LeftParen,
                    ..
                }),
            ) if profile == SyntaxProfile::Structured => Some((name, span, "symbol")),
            _ => None,
        };
        if let Some((name, span, kind)) = candidate {
            entries.push(entry(
                name,
                kind,
                &format!("{kind} {name}"),
                span.clone(),
                span.start,
                source.text().len(),
            ));
        }
    }
}

fn entry(
    name: &str,
    kind: &str,
    detail: &str,
    span: SourceSpan,
    available_start: usize,
    available_end: usize,
) -> SymbolEntry {
    SymbolEntry {
        name: name.to_owned(),
        kind: kind.to_owned(),
        detail: detail.to_owned(),
        insert_text: name.to_owned(),
        declaration_span: Some(span),
        available_start,
        available_end,
    }
}

fn symbol(
    name: &str,
    kind: &str,
    detail: &str,
    span: SourceSpan,
    children: Vec<DocumentSymbol>,
) -> DocumentSymbol {
    DocumentSymbol {
        name: name.to_owned(),
        kind: kind.to_owned(),
        detail: detail.to_owned(),
        span,
        children,
    }
}

#[allow(clippy::too_many_arguments)]
fn add_completion(
    completions: &mut Vec<CompletionItem>,
    labels: &mut HashSet<String>,
    prefix: &str,
    label: &str,
    kind: &str,
    detail: &str,
    insert_text: &str,
    replacement_span: &SourceSpan,
) {
    if !label.starts_with(prefix) || !labels.insert(label.to_owned()) {
        return;
    }
    completions.push(CompletionItem {
        label: label.to_owned(),
        kind: kind.to_owned(),
        detail: detail.to_owned(),
        insert_text: insert_text.to_owned(),
        replacement_span: replacement_span.clone(),
    });
}

fn identifier_span(
    source: &SourceFile,
    container: &SourceSpan,
    name: &str,
    search_start: usize,
) -> SourceSpan {
    let start = container.start.max(search_start);
    let end = container.end.min(source.text().len());
    let offset = source
        .text()
        .get(start..end)
        .and_then(|slice| slice.find(name).map(|relative| start + relative))
        .unwrap_or(container.start);
    span_for_offsets(source, offset, offset.saturating_add(name.len()))
}

fn word_span(source: &SourceFile, cursor: usize) -> SourceSpan {
    let bytes = source.text().as_bytes();
    let mut start = cursor;
    while start > 0 && is_identifier_byte(bytes[start - 1]) {
        start -= 1;
    }
    let mut end = cursor;
    while end < bytes.len() && is_identifier_byte(bytes[end]) {
        end += 1;
    }
    span_for_offsets(source, start, end)
}

fn span_for_offsets(source: &SourceFile, start: usize, end: usize) -> SourceSpan {
    let (start_line, start_column) = line_column(source.text(), start);
    let (end_line, end_column) = line_column(source.text(), end);
    SourceSpan::new(
        source.name(),
        start,
        end,
        start_line,
        start_column,
        end_line,
        end_column,
    )
}

fn line_column(source: &str, offset: usize) -> (usize, usize) {
    let mut line = 1;
    let mut column = 1;
    for character in source[..offset.min(source.len())].chars() {
        if character == '\n' {
            line += 1;
            column = 1;
        } else {
            column += 1;
        }
    }
    (line, column)
}

fn is_identifier_byte(value: u8) -> bool {
    value.is_ascii_alphanumeric() || value == b'_'
}

fn inferred_type(expression: &Expression) -> &'static str {
    match expression.kind {
        ExpressionKind::Integer(_) => "Int",
        ExpressionKind::Float(_) => "Float",
        ExpressionKind::Bool(_) => "Bool",
        ExpressionKind::String(_) => "String",
        ExpressionKind::Nil => "Nil",
        ExpressionKind::Call { .. } => "inferred result",
        _ => "inferred value",
    }
}

fn keyword_items(profile: SyntaxProfile) -> &'static [(&'static str, &'static str)] {
    match profile {
        SyntaxProfile::Guided => &[
            ("action", "Declare a reusable action"),
            ("class", "Declare a class"),
            ("else", "Alternative conditional branch"),
            ("end", "Close a Guided block"),
            ("false", "Boolean false value"),
            ("has", "Declare an inferred variable"),
            ("if", "Start a conditional block"),
            ("import", "Import an explicit foreign module"),
            ("nil", "Absence of a value"),
            ("repeat", "Repeat a block a fixed number of times"),
            ("return", "Return from the current action"),
            ("true", "Boolean true value"),
            ("while", "Repeat while a condition is true"),
        ],
        SyntaxProfile::Structured => &[
            ("auto", "Inferred surface type"),
            ("class", "Declare a class"),
            ("else", "Alternative conditional branch"),
            ("false", "Boolean false value"),
            ("if", "Start a conditional block"),
            ("import", "Import an explicit foreign module"),
            ("nil", "Absence of a value"),
            ("repeat", "Repeat a block a fixed number of times"),
            ("return", "Return from the current action"),
            ("true", "Boolean true value"),
            ("while", "Repeat while a condition is true"),
        ],
        SyntaxProfile::Advanced => &[
            ("class", "Declare an indentation-based class"),
            ("else", "Alternative conditional branch"),
            ("false", "Boolean false value"),
            ("if", "Start an indentation-based conditional"),
            ("import", "Import an explicit foreign module"),
            ("nil", "Absence of a value"),
            ("repeat", "Repeat a block a fixed number of times"),
            ("return", "Return from the current action"),
            ("true", "Boolean true value"),
            ("while", "Repeat while a condition is true"),
        ],
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn completion_includes_visible_symbols_and_builtins() {
        let source = SourceFile::new(
            "test.spr",
            "has name = \"Sprout\"\naction greet(person)\n    print(person)\nend\nprint(na)\n",
        );
        let cursor = source.text().rfind("na").unwrap() + 2;
        let result = inspect(&source, SyntaxProfile::Guided, cursor);
        assert!(result.diagnostics.is_empty(), "{:?}", result.diagnostics);
        assert!(result.completions.iter().any(|item| item.label == "name"));
        assert!(!result.completions.iter().any(|item| item.label == "person"));
        assert_eq!(result.completions[0].replacement_span.start, cursor - 2);
    }

    #[test]
    fn hover_and_definition_resolve_function_parameters() {
        let source = SourceFile::new("test.spr", "action greet(person)\n    print(person)\nend\n");
        let cursor = source.text().rfind("person").unwrap() + 2;
        let result = inspect(&source, SyntaxProfile::Guided, cursor);
        assert_eq!(result.hover.unwrap().contents, "parameter person");
        let definition = result.definition.unwrap();
        assert_eq!(definition.span.start_line, 1);
        assert_eq!(
            &source.text()[definition.span.start..definition.span.end],
            "person"
        );
    }

    #[test]
    fn outline_retains_action_and_class_hierarchy() {
        let source = SourceFile::new(
            "test.spr",
            "class Greeter\n  action greet(name)\n    print(name)\n  end\nend\n",
        );
        let result = inspect(&source, SyntaxProfile::Guided, 0);
        assert_eq!(result.symbols[0].name, "Greeter");
        assert_eq!(result.symbols[0].children[0].name, "greet");
    }
}
