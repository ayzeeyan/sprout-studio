use crate::source::SourceSpan;
use std::fmt;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Severity {
    Error,
    Warning,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Diagnostic {
    pub code: &'static str,
    pub severity: Severity,
    pub title: String,
    pub message: String,
    pub span: Box<SourceSpan>,
    pub related_information: Box<Vec<RelatedInformation>>,
    pub suggestions: Vec<String>,
    pub optional_fixes: Box<Vec<DiagnosticFix>>,
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct RelatedInformation {
    pub message: String,
    pub span: Box<SourceSpan>,
}

impl RelatedInformation {
    pub fn new(message: impl Into<String>, span: SourceSpan) -> Self {
        Self {
            message: message.into(),
            span: Box::new(span),
        }
    }
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct DiagnosticFix {
    pub title: String,
    pub replacement: String,
    pub span: Box<SourceSpan>,
}

impl DiagnosticFix {
    pub fn replacement(
        title: impl Into<String>,
        replacement: impl Into<String>,
        span: SourceSpan,
    ) -> Self {
        Self {
            title: title.into(),
            replacement: replacement.into(),
            span: Box::new(span),
        }
    }
}

impl Diagnostic {
    pub fn error(
        code: &'static str,
        title: impl Into<String>,
        message: impl Into<String>,
        span: SourceSpan,
    ) -> Self {
        Self {
            code,
            severity: Severity::Error,
            title: title.into(),
            message: message.into(),
            span: Box::new(span),
            related_information: Box::default(),
            suggestions: Vec::new(),
            optional_fixes: Box::default(),
        }
    }
}

impl fmt::Display for Diagnostic {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "{}:{}:{}: {}: {} [{}]",
            self.span.file,
            self.span.start_line,
            self.span.start_column,
            self.title,
            self.message,
            self.code
        )
    }
}
