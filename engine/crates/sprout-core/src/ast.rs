use crate::source::SourceSpan;

/// The syntax-profile-independent representation of a Sprout program.
#[derive(Clone, Debug, PartialEq)]
pub struct Program {
    pub statements: Vec<Statement>,
}

#[derive(Clone, Debug, PartialEq)]
pub struct Node<T> {
    pub kind: T,
    pub span: SourceSpan,
}

impl<T> Node<T> {
    pub fn new(kind: T, span: SourceSpan) -> Self {
        Self { kind, span }
    }
}

pub type Statement = Node<StatementKind>;
pub type Expression = Node<ExpressionKind>;

#[derive(Clone, Debug, PartialEq)]
pub enum StatementKind {
    Variable {
        name: String,
        initializer: Expression,
    },
    Assignment {
        name: String,
        value: Expression,
    },
    Expression(Expression),
    If {
        condition: Expression,
        then_branch: Vec<Statement>,
        else_branch: Vec<Statement>,
    },
    Repeat {
        count: Expression,
        body: Vec<Statement>,
    },
    While {
        condition: Expression,
        body: Vec<Statement>,
    },
    Function {
        name: String,
        parameters: Vec<String>,
        body: Vec<Statement>,
    },
    Class {
        name: String,
        methods: Vec<Statement>,
    },
    Return(Option<Expression>),
}

#[derive(Clone, Debug, PartialEq)]
pub enum ExpressionKind {
    Integer(i64),
    Float(f64),
    Bool(bool),
    String(String),
    Nil,
    Variable(String),
    Unary {
        operator: UnaryOperator,
        operand: Box<Expression>,
    },
    Binary {
        left: Box<Expression>,
        operator: BinaryOperator,
        right: Box<Expression>,
    },
    Call {
        callee: Box<Expression>,
        arguments: Vec<Expression>,
    },
    Member {
        object: Box<Expression>,
        name: String,
        member_span: SourceSpan,
    },
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum UnaryOperator {
    Negate,
    Not,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum BinaryOperator {
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    Equal,
    NotEqual,
    Less,
    LessEqual,
    Greater,
    GreaterEqual,
    And,
    Or,
}
