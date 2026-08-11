use crate::ast::{
    BinaryOperator, Expression, ExpressionKind, Program, Statement, StatementKind, UnaryOperator,
};
use crate::diagnostic::Diagnostic;
use crate::source::SourceSpan;
use std::cell::RefCell;
use std::collections::HashMap;
use std::fmt;
use std::rc::Rc;

#[derive(Clone)]
pub enum Value {
    Int(i64),
    Float(f64),
    Bool(bool),
    String(String),
    Nil,
    Function(Rc<Function>),
    NativePrint,
}

impl fmt::Debug for Value {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "{self}")
    }
}

impl fmt::Display for Value {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Int(value) => write!(formatter, "{value}"),
            Self::Float(value) => write!(formatter, "{value}"),
            Self::Bool(value) => write!(formatter, "{value}"),
            Self::String(value) => formatter.write_str(value),
            Self::Nil => formatter.write_str("nil"),
            Self::Function(function) => write!(formatter, "<action {}>", function.name),
            Self::NativePrint => formatter.write_str("<native action print>"),
        }
    }
}

impl PartialEq for Value {
    fn eq(&self, other: &Self) -> bool {
        match (self, other) {
            (Self::Int(left), Self::Int(right)) => left == right,
            (Self::Float(left), Self::Float(right)) => left == right,
            (Self::Int(left), Self::Float(right)) => *left as f64 == *right,
            (Self::Float(left), Self::Int(right)) => *left == *right as f64,
            (Self::Bool(left), Self::Bool(right)) => left == right,
            (Self::String(left), Self::String(right)) => left == right,
            (Self::Nil, Self::Nil) => true,
            _ => false,
        }
    }
}

pub struct RunResult {
    pub output: Vec<String>,
    pub diagnostic: Option<Diagnostic>,
}

pub fn run(program: &Program) -> RunResult {
    let mut interpreter = Interpreter::new();
    let diagnostic = interpreter
        .execute_block(&program.statements, interpreter.globals.clone())
        .err();
    RunResult {
        output: interpreter.output,
        diagnostic,
    }
}

#[derive(Clone)]
pub struct Function {
    name: String,
    parameters: Vec<String>,
    body: Vec<Statement>,
    closure: Environment,
}

#[derive(Clone)]
struct Environment(Rc<RefCell<Scope>>);

struct Scope {
    values: HashMap<String, Value>,
    parent: Option<Environment>,
}

impl Environment {
    fn new(parent: Option<Environment>) -> Self {
        Self(Rc::new(RefCell::new(Scope {
            values: HashMap::new(),
            parent,
        })))
    }

    fn define(&self, name: String, value: Value) {
        self.0.borrow_mut().values.insert(name, value);
    }

    fn get(&self, name: &str) -> Option<Value> {
        let scope = self.0.borrow();
        scope
            .values
            .get(name)
            .cloned()
            .or_else(|| scope.parent.as_ref().and_then(|parent| parent.get(name)))
    }

    fn assign(&self, name: &str, value: Value) -> bool {
        if self.0.borrow().values.contains_key(name) {
            self.0.borrow_mut().values.insert(name.to_owned(), value);
            true
        } else if let Some(parent) = &self.0.borrow().parent {
            parent.assign(name, value)
        } else {
            false
        }
    }
}

struct Interpreter {
    globals: Environment,
    environment: Environment,
    output: Vec<String>,
}

enum Flow {
    Continue,
    Return(Value),
}

type RuntimeResult<T> = Result<T, Diagnostic>;

impl Interpreter {
    fn new() -> Self {
        let globals = Environment::new(None);
        globals.define("print".into(), Value::NativePrint);
        Self {
            globals: globals.clone(),
            environment: globals,
            output: Vec::new(),
        }
    }

    fn execute_block(
        &mut self,
        statements: &[Statement],
        environment: Environment,
    ) -> RuntimeResult<Flow> {
        let previous = std::mem::replace(&mut self.environment, environment);
        let result = (|| {
            for statement in statements {
                if let Flow::Return(value) = self.execute(statement)? {
                    return Ok(Flow::Return(value));
                }
            }
            Ok(Flow::Continue)
        })();
        self.environment = previous;
        result
    }

    fn execute(&mut self, statement: &Statement) -> RuntimeResult<Flow> {
        match &statement.kind {
            StatementKind::Variable { name, initializer } => {
                let value = self.evaluate(initializer)?;
                self.environment.define(name.clone(), value);
            }
            StatementKind::Assignment { name, value } => {
                let value = self.evaluate(value)?;
                if !self.environment.assign(name, value) {
                    return Err(error(
                        "SPT201",
                        "Unknown variable",
                        format!("`{name}` has not been declared."),
                        &statement.span,
                    ));
                }
            }
            StatementKind::Expression(expression) => {
                self.evaluate(expression)?;
            }
            StatementKind::If {
                condition,
                then_branch,
                else_branch,
            } => {
                let branch = if self.evaluate(condition)?.truthy() {
                    then_branch
                } else {
                    else_branch
                };
                if let Flow::Return(value) =
                    self.execute_block(branch, Environment::new(Some(self.environment.clone())))?
                {
                    return Ok(Flow::Return(value));
                }
            }
            StatementKind::Repeat { count, body } => {
                let Value::Int(count) = self.evaluate(count)? else {
                    return Err(error(
                        "SPT207",
                        "Invalid repeat count",
                        "A repeat count must be an Int.",
                        &statement.span,
                    ));
                };
                if count < 0 {
                    return Err(error(
                        "SPT207",
                        "Invalid repeat count",
                        "A repeat count cannot be negative.",
                        &statement.span,
                    ));
                }
                for _ in 0..count {
                    if let Flow::Return(value) =
                        self.execute_block(body, Environment::new(Some(self.environment.clone())))?
                    {
                        return Ok(Flow::Return(value));
                    }
                }
            }
            StatementKind::While { condition, body } => {
                while self.evaluate(condition)?.truthy() {
                    if let Flow::Return(value) =
                        self.execute_block(body, Environment::new(Some(self.environment.clone())))?
                    {
                        return Ok(Flow::Return(value));
                    }
                }
            }
            StatementKind::Function {
                name,
                parameters,
                body,
            } => {
                let function = Function {
                    name: name.clone(),
                    parameters: parameters.clone(),
                    body: body.clone(),
                    closure: self.environment.clone(),
                };
                self.environment
                    .define(name.clone(), Value::Function(Rc::new(function)));
            }
            StatementKind::Return(value) => {
                let value = value
                    .as_ref()
                    .map(|expression| self.evaluate(expression))
                    .transpose()?
                    .unwrap_or(Value::Nil);
                return Ok(Flow::Return(value));
            }
        }
        Ok(Flow::Continue)
    }

    fn evaluate(&mut self, expression: &Expression) -> RuntimeResult<Value> {
        match &expression.kind {
            ExpressionKind::Integer(value) => Ok(Value::Int(*value)),
            ExpressionKind::Float(value) => Ok(Value::Float(*value)),
            ExpressionKind::Bool(value) => Ok(Value::Bool(*value)),
            ExpressionKind::String(value) => Ok(Value::String(value.clone())),
            ExpressionKind::Nil => Ok(Value::Nil),
            ExpressionKind::Variable(name) => self.environment.get(name).ok_or_else(|| {
                error(
                    "SPT201",
                    "Unknown variable",
                    format!("`{name}` has not been declared."),
                    &expression.span,
                )
            }),
            ExpressionKind::Unary { operator, operand } => {
                let value = self.evaluate(operand)?;
                match (operator, value) {
                    (UnaryOperator::Negate, Value::Int(value)) => value
                        .checked_neg()
                        .map(Value::Int)
                        .ok_or_else(|| overflow(&expression.span)),
                    (UnaryOperator::Negate, Value::Float(value)) => Ok(Value::Float(-value)),
                    (UnaryOperator::Not, value) => Ok(Value::Bool(!value.truthy())),
                    _ => Err(type_error(
                        "This value cannot be negated.",
                        &expression.span,
                    )),
                }
            }
            ExpressionKind::Binary {
                left,
                operator,
                right,
            } => {
                if *operator == BinaryOperator::And {
                    let left = self.evaluate(left)?;
                    return if left.truthy() {
                        self.evaluate(right)
                    } else {
                        Ok(left)
                    };
                }
                if *operator == BinaryOperator::Or {
                    let left = self.evaluate(left)?;
                    return if left.truthy() {
                        Ok(left)
                    } else {
                        self.evaluate(right)
                    };
                }
                let left = self.evaluate(left)?;
                let right = self.evaluate(right)?;
                self.binary(left, *operator, right, &expression.span)
            }
            ExpressionKind::Call { callee, arguments } => {
                let callee = self.evaluate(callee)?;
                let arguments = arguments
                    .iter()
                    .map(|argument| self.evaluate(argument))
                    .collect::<RuntimeResult<Vec<_>>>()?;
                self.call(callee, arguments, &expression.span)
            }
        }
    }

    fn call(
        &mut self,
        callee: Value,
        arguments: Vec<Value>,
        span: &SourceSpan,
    ) -> RuntimeResult<Value> {
        match callee {
            Value::NativePrint => {
                let line = arguments
                    .iter()
                    .map(ToString::to_string)
                    .collect::<Vec<_>>()
                    .join(" ");
                self.output.push(line);
                Ok(Value::Nil)
            }
            Value::Function(function) => {
                if arguments.len() != function.parameters.len() {
                    return Err(error(
                        "SPT205",
                        "Wrong number of arguments",
                        format!(
                            "`{}` expects {} argument(s), but received {}.",
                            function.name,
                            function.parameters.len(),
                            arguments.len()
                        ),
                        span,
                    ));
                }
                let environment = Environment::new(Some(function.closure.clone()));
                for (name, value) in function.parameters.iter().zip(arguments) {
                    environment.define(name.clone(), value);
                }
                match self.execute_block(&function.body, environment)? {
                    Flow::Return(value) => Ok(value),
                    Flow::Continue => Ok(Value::Nil),
                }
            }
            _ => Err(error(
                "SPT206",
                "Value is not callable",
                "Only actions can be called.",
                span,
            )),
        }
    }

    fn binary(
        &self,
        left: Value,
        operator: BinaryOperator,
        right: Value,
        span: &SourceSpan,
    ) -> RuntimeResult<Value> {
        use BinaryOperator as Op;
        match (left, operator, right) {
            (Value::Int(a), Op::Add, Value::Int(b)) => a
                .checked_add(b)
                .map(Value::Int)
                .ok_or_else(|| overflow(span)),
            (Value::Int(a), Op::Subtract, Value::Int(b)) => a
                .checked_sub(b)
                .map(Value::Int)
                .ok_or_else(|| overflow(span)),
            (Value::Int(a), Op::Multiply, Value::Int(b)) => a
                .checked_mul(b)
                .map(Value::Int)
                .ok_or_else(|| overflow(span)),
            (Value::Int(_), Op::Divide | Op::Remainder, Value::Int(0)) => Err(error(
                "SPT203",
                "Division by zero",
                "The divisor cannot be zero.",
                span,
            )),
            (Value::Int(a), Op::Divide, Value::Int(b)) => a
                .checked_div(b)
                .map(Value::Int)
                .ok_or_else(|| overflow(span)),
            (Value::Int(a), Op::Remainder, Value::Int(b)) => a
                .checked_rem(b)
                .map(Value::Int)
                .ok_or_else(|| overflow(span)),
            (Value::String(mut a), Op::Add, Value::String(b)) => {
                a.push_str(&b);
                Ok(Value::String(a))
            }
            (left, Op::Equal, right) => Ok(Value::Bool(left == right)),
            (left, Op::NotEqual, right) => Ok(Value::Bool(left != right)),
            (left, operator @ (Op::Add | Op::Subtract | Op::Multiply | Op::Divide), right)
                if left.as_number().is_some() && right.as_number().is_some() =>
            {
                let a = left.as_number().unwrap();
                let b = right.as_number().unwrap();
                if operator == Op::Divide && b == 0.0 {
                    return Err(error(
                        "SPT203",
                        "Division by zero",
                        "The divisor cannot be zero.",
                        span,
                    ));
                }
                Ok(Value::Float(match operator {
                    Op::Add => a + b,
                    Op::Subtract => a - b,
                    Op::Multiply => a * b,
                    Op::Divide => a / b,
                    _ => unreachable!(),
                }))
            }
            (
                left,
                operator @ (Op::Less | Op::LessEqual | Op::Greater | Op::GreaterEqual),
                right,
            ) if left.as_number().is_some() && right.as_number().is_some() => {
                let a = left.as_number().unwrap();
                let b = right.as_number().unwrap();
                Ok(Value::Bool(match operator {
                    Op::Less => a < b,
                    Op::LessEqual => a <= b,
                    Op::Greater => a > b,
                    Op::GreaterEqual => a >= b,
                    _ => unreachable!(),
                }))
            }
            _ => Err(type_error(
                "These values do not support this operator.",
                span,
            )),
        }
    }
}

impl Value {
    fn truthy(&self) -> bool {
        !matches!(self, Self::Bool(false) | Self::Nil)
    }

    fn as_number(&self) -> Option<f64> {
        match self {
            Self::Int(value) => Some(*value as f64),
            Self::Float(value) => Some(*value),
            _ => None,
        }
    }
}

fn error(
    code: &'static str,
    title: impl Into<String>,
    message: impl Into<String>,
    span: &SourceSpan,
) -> Diagnostic {
    Diagnostic::error(code, title, message, span.clone())
}

fn type_error(message: &str, span: &SourceSpan) -> Diagnostic {
    error("SPT202", "Type error", message, span)
}

fn overflow(span: &SourceSpan) -> Diagnostic {
    error(
        "SPT208",
        "Integer overflow",
        "This calculation is outside the range of a Sprout Int.",
        span,
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::parser::parse;
    use crate::source::SourceFile;

    fn execute(text: &str) -> RunResult {
        let parsed = parse(&SourceFile::new("test.spr", text));
        assert!(parsed.diagnostics.is_empty(), "{:?}", parsed.diagnostics);
        run(&parsed.program.unwrap())
    }

    #[test]
    fn executes_first_iteration_program() {
        let result = execute(
            "has name = \"Sprout\"\nhas count = 3\naction greet(person)\n  print(\"Hello \" + person)\nend\nrepeat count times\n  greet(name)\nend\n",
        );
        assert!(result.diagnostic.is_none(), "{:?}", result.diagnostic);
        assert_eq!(result.output, vec!["Hello Sprout"; 3]);
    }

    #[test]
    fn supports_assignment_conditions_and_while_loops() {
        let result = execute(
            "has n = 0\nwhile n < 3\n  n = n + 1\nend\nif n == 3\n  print(\"done\")\nelse\n  print(\"wrong\")\nend\n",
        );
        assert!(result.diagnostic.is_none(), "{:?}", result.diagnostic);
        assert_eq!(result.output, vec!["done"]);
    }

    #[test]
    fn functions_return_values_with_lexical_scope() {
        let result = execute(
            "has prefix = \"Hi \"\naction greeting(name)\n  return prefix + name\nend\nprint(greeting(\"Ada\"))\n",
        );
        assert!(result.diagnostic.is_none(), "{:?}", result.diagnostic);
        assert_eq!(result.output, vec!["Hi Ada"]);
    }

    #[test]
    fn unknown_variable_is_a_structured_diagnostic() {
        let result = execute("print(missing)\n");
        let diagnostic = result.diagnostic.unwrap();
        assert_eq!(diagnostic.code, "SPT201");
        assert_eq!(diagnostic.span.start_column, 7);
    }

    #[test]
    fn invalid_operand_types_do_not_panic() {
        let result = execute("print(\"one\" - 1)\n");
        assert_eq!(result.diagnostic.unwrap().code, "SPT202");
    }
}
