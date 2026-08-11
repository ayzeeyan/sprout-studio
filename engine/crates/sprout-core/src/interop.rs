//! Host-neutral values and services used by explicit language interoperability.

use std::any::Any;
use std::collections::BTreeMap;
use std::fmt;
use std::rc::Rc;

#[derive(Clone, Debug)]
pub enum ForeignValue {
    Int(i64),
    Float(f64),
    Bool(bool),
    String(String),
    Nil,
    List(Vec<ForeignValue>),
    Map(BTreeMap<String, ForeignValue>),
    Object(Rc<dyn ForeignObject>),
}

#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ForeignError {
    pub exception_type: String,
    pub message: String,
}

impl ForeignError {
    pub fn new(exception_type: impl Into<String>, message: impl Into<String>) -> Self {
        Self {
            exception_type: exception_type.into(),
            message: message.into(),
        }
    }
}

impl fmt::Display for ForeignError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        if self.message.is_empty() {
            formatter.write_str(&self.exception_type)
        } else {
            write!(formatter, "{}: {}", self.exception_type, self.message)
        }
    }
}

impl std::error::Error for ForeignError {}

pub trait ForeignObject: fmt::Debug {
    fn as_any(&self) -> &dyn Any;

    fn type_name(&self) -> String;

    fn get_member(&self, name: &str) -> Result<ForeignValue, ForeignError>;

    fn get_index(&self, index: &ForeignValue) -> Result<ForeignValue, ForeignError>;

    fn call(&self, arguments: &[ForeignValue]) -> Result<ForeignValue, ForeignError>;

    fn display(&self) -> Result<String, ForeignError>;
}

pub trait PythonHost {
    fn import_module(&mut self, module: &str) -> Result<ForeignValue, ForeignError>;
}
