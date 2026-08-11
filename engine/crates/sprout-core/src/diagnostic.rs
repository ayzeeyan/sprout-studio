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
    pub suggestions: Vec<String>,
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
            suggestions: Vec::new(),
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
