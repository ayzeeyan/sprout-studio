use crate::source::SourceSpan;

/// The syntax-profile-independent representation of a Sprout program.
#[derive(Clone, Debug, PartialEq)]
pub struct Program {
    pub statements: Vec<Statement>,
}

impl Program {
    /// Compares canonical program meaning while ignoring source-profile spans.
    pub fn semantically_equivalent(&self, other: &Self) -> bool {
        equivalent_statements(&self.statements, &other.statements)
    }
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

fn equivalent_statements(left: &[Statement], right: &[Statement]) -> bool {
    left.len() == right.len()
        && left
            .iter()
            .zip(right)
            .all(|(left, right)| equivalent_statement(&left.kind, &right.kind))
}

fn equivalent_statement(left: &StatementKind, right: &StatementKind) -> bool {
    match (left, right) {
        (
            StatementKind::Variable {
                name: left_name,
                initializer: left_value,
            },
            StatementKind::Variable {
                name: right_name,
                initializer: right_value,
            },
        ) => left_name == right_name && equivalent_expression(left_value, right_value),
        (
            StatementKind::Assignment {
                name: left_name,
                value: left_value,
            },
            StatementKind::Assignment {
                name: right_name,
                value: right_value,
            },
        ) => left_name == right_name && equivalent_expression(left_value, right_value),
        (StatementKind::Expression(left), StatementKind::Expression(right)) => {
            equivalent_expression(left, right)
        }
        (
            StatementKind::If {
                condition: left_condition,
                then_branch: left_then,
                else_branch: left_else,
            },
            StatementKind::If {
                condition: right_condition,
                then_branch: right_then,
                else_branch: right_else,
            },
        ) => {
            equivalent_expression(left_condition, right_condition)
                && equivalent_statements(left_then, right_then)
                && equivalent_statements(left_else, right_else)
        }
        (
            StatementKind::Repeat {
                count: left_count,
                body: left_body,
            },
            StatementKind::Repeat {
                count: right_count,
                body: right_body,
            },
        ) => {
            equivalent_expression(left_count, right_count)
                && equivalent_statements(left_body, right_body)
        }
        (
            StatementKind::While {
                condition: left_condition,
                body: left_body,
            },
            StatementKind::While {
                condition: right_condition,
                body: right_body,
            },
        ) => {
            equivalent_expression(left_condition, right_condition)
                && equivalent_statements(left_body, right_body)
        }
        (
            StatementKind::Function {
                name: left_name,
                parameters: left_parameters,
                body: left_body,
            },
            StatementKind::Function {
                name: right_name,
                parameters: right_parameters,
                body: right_body,
            },
        ) => {
            left_name == right_name
                && left_parameters == right_parameters
                && equivalent_statements(left_body, right_body)
        }
        (
            StatementKind::Class {
                name: left_name,
                methods: left_methods,
            },
            StatementKind::Class {
                name: right_name,
                methods: right_methods,
            },
        ) => left_name == right_name && equivalent_statements(left_methods, right_methods),
        (StatementKind::Return(left), StatementKind::Return(right)) => match (left, right) {
            (Some(left), Some(right)) => equivalent_expression(left, right),
            (None, None) => true,
            _ => false,
        },
        _ => false,
    }
}

fn equivalent_expression(left: &Expression, right: &Expression) -> bool {
    match (&left.kind, &right.kind) {
        (ExpressionKind::Integer(left), ExpressionKind::Integer(right)) => left == right,
        (ExpressionKind::Float(left), ExpressionKind::Float(right)) => left == right,
        (ExpressionKind::Bool(left), ExpressionKind::Bool(right)) => left == right,
        (ExpressionKind::String(left), ExpressionKind::String(right)) => left == right,
        (ExpressionKind::Nil, ExpressionKind::Nil) => true,
        (ExpressionKind::Variable(left), ExpressionKind::Variable(right)) => left == right,
        (
            ExpressionKind::Unary {
                operator: left_operator,
                operand: left_operand,
            },
            ExpressionKind::Unary {
                operator: right_operator,
                operand: right_operand,
            },
        ) => left_operator == right_operator && equivalent_expression(left_operand, right_operand),
        (
            ExpressionKind::Binary {
                left: left_left,
                operator: left_operator,
                right: left_right,
            },
            ExpressionKind::Binary {
                left: right_left,
                operator: right_operator,
                right: right_right,
            },
        ) => {
            left_operator == right_operator
                && equivalent_expression(left_left, right_left)
                && equivalent_expression(left_right, right_right)
        }
        (
            ExpressionKind::Call {
                callee: left_callee,
                arguments: left_arguments,
            },
            ExpressionKind::Call {
                callee: right_callee,
                arguments: right_arguments,
            },
        ) => {
            equivalent_expression(left_callee, right_callee)
                && left_arguments.len() == right_arguments.len()
                && left_arguments
                    .iter()
                    .zip(right_arguments)
                    .all(|(left, right)| equivalent_expression(left, right))
        }
        (
            ExpressionKind::Member {
                object: left_object,
                name: left_name,
                ..
            },
            ExpressionKind::Member {
                object: right_object,
                name: right_name,
                ..
            },
        ) => left_name == right_name && equivalent_expression(left_object, right_object),
        _ => false,
    }
}
