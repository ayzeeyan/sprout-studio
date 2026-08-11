use std::sync::Arc;

/// Immutable source text owned by the engine.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SourceFile {
    name: Arc<str>,
    text: Arc<str>,
}

impl SourceFile {
    pub fn new(name: impl Into<Arc<str>>, text: impl Into<Arc<str>>) -> Self {
        Self {
            name: name.into(),
            text: text.into(),
        }
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn text(&self) -> &str {
        &self.text
    }
}

/// A half-open byte range with one-based line and column locations.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SourceSpan {
    pub file: Arc<str>,
    pub start: usize,
    pub end: usize,
    pub start_line: usize,
    pub start_column: usize,
    pub end_line: usize,
    pub end_column: usize,
}

impl SourceSpan {
    pub fn new(
        file: impl Into<Arc<str>>,
        start: usize,
        end: usize,
        start_line: usize,
        start_column: usize,
        end_line: usize,
        end_column: usize,
    ) -> Self {
        Self {
            file: file.into(),
            start,
            end,
            start_line,
            start_column,
            end_line,
            end_column,
        }
    }

    pub fn merge(&self, other: &Self) -> Self {
        debug_assert_eq!(self.file, other.file);
        Self::new(
            self.file.clone(),
            self.start,
            other.end,
            self.start_line,
            self.start_column,
            other.end_line,
            other.end_column,
        )
    }
}
