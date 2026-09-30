//! A zero-dependency JSON parser and serializer for Sprout runtime values.

use crate::runtime::Value;
use std::collections::BTreeMap;
use std::iter::Peekable;
use std::str::Chars;

/// Parses a JSON string into a Sprout `Value`.
pub fn parse(input: &str) -> Result<Value, String> {
    let mut parser = JsonParser::new(input);
    let value = parser.parse_value()?;
    parser.skip_whitespace();
    if parser.chars.peek().is_some() {
        return Err("Unexpected trailing characters after JSON value".into());
    }
    Ok(value)
}

/// Checks whether a string is valid JSON.
pub fn is_valid(input: &str) -> bool {
    parse(input).is_ok()
}

/// Serializes a Sprout `Value` into a JSON formatted string.
pub fn stringify(value: &Value) -> String {
    let mut output = String::new();
    serialize_value(value, &mut output);
    output
}

fn serialize_value(value: &Value, output: &mut String) {
    match value {
        Value::Nil => output.push_str("null"),
        Value::Bool(true) => output.push_str("true"),
        Value::Bool(false) => output.push_str("false"),
        Value::Int(n) => output.push_str(&n.to_string()),
        Value::Float(f) => {
            if f.is_nan() || f.is_infinite() {
                output.push_str("null");
            } else {
                let s = f.to_string();
                output.push_str(&s);
                if !s.contains('.') && !s.contains('e') && !s.contains('E') {
                    output.push_str(".0");
                }
            }
        }
        Value::String(s) => escape_string(s, output),
        Value::List(items) => {
            output.push('[');
            for (i, item) in items.iter().enumerate() {
                if i > 0 {
                    output.push_str(", ");
                }
                serialize_value(item, output);
            }
            output.push(']');
        }
        Value::Map(entries) => {
            output.push('{');
            for (i, (key, val)) in entries.iter().enumerate() {
                if i > 0 {
                    output.push_str(", ");
                }
                escape_string(key, output);
                output.push_str(": ");
                serialize_value(val, output);
            }
            output.push('}');
        }
        _ => output.push_str("null"),
    }
}

fn escape_string(s: &str, output: &mut String) {
    output.push('"');
    for c in s.chars() {
        match c {
            '"' => output.push_str("\\\""),
            '\\' => output.push_str("\\\\"),
            '\n' => output.push_str("\\n"),
            '\r' => output.push_str("\\r"),
            '\t' => output.push_str("\\t"),
            '\u{08}' => output.push_str("\\b"),
            '\u{0C}' => output.push_str("\\f"),
            c if (c as u32) < 0x20 => {
                output.push_str(&format!("\\u{:04x}", c as u32));
            }
            c => output.push(c),
        }
    }
    output.push('"');
}

struct JsonParser<'a> {
    chars: Peekable<Chars<'a>>,
}

impl<'a> JsonParser<'a> {
    fn new(input: &'a str) -> Self {
        Self {
            chars: input.chars().peekable(),
        }
    }

    fn skip_whitespace(&mut self) {
        while let Some(&c) = self.chars.peek() {
            if c.is_whitespace() {
                self.chars.next();
            } else {
                break;
            }
        }
    }

    fn parse_value(&mut self) -> Result<Value, String> {
        self.skip_whitespace();
        match self.chars.peek() {
            Some(&'n') => self.parse_null(),
            Some(&'t') | Some(&'f') => self.parse_bool(),
            Some(&'"') => self.parse_string().map(Value::String),
            Some(&'[') => self.parse_array(),
            Some(&'{') => self.parse_object(),
            Some(&c) if c == '-' || c.is_ascii_digit() => self.parse_number(),
            Some(&other) => Err(format!("Unexpected character `{other}` in JSON")),
            None => Err("Unexpected end of JSON input".into()),
        }
    }

    fn parse_null(&mut self) -> Result<Value, String> {
        for expected in ['n', 'u', 'l', 'l'] {
            match self.chars.next() {
                Some(c) if c == expected => {}
                _ => return Err("Expected `null`".into()),
            }
        }
        Ok(Value::Nil)
    }

    fn parse_bool(&mut self) -> Result<Value, String> {
        if let Some(&'t') = self.chars.peek() {
            for expected in ['t', 'r', 'u', 'e'] {
                match self.chars.next() {
                    Some(c) if c == expected => {}
                    _ => return Err("Expected `true`".into()),
                }
            }
            Ok(Value::Bool(true))
        } else {
            for expected in ['f', 'a', 'l', 's', 'e'] {
                match self.chars.next() {
                    Some(c) if c == expected => {}
                    _ => return Err("Expected `false`".into()),
                }
            }
            Ok(Value::Bool(false))
        }
    }

    fn parse_string(&mut self) -> Result<String, String> {
        if self.chars.next() != Some('"') {
            return Err("Expected `\"` at start of string".into());
        }
        let mut result = String::new();
        while let Some(c) = self.chars.next() {
            match c {
                '"' => return Ok(result),
                '\\' => match self.chars.next() {
                    Some('"') => result.push('"'),
                    Some('\\') => result.push('\\'),
                    Some('/') => result.push('/'),
                    Some('b') => result.push('\u{08}'),
                    Some('f') => result.push('\u{0C}'),
                    Some('n') => result.push('\n'),
                    Some('r') => result.push('\r'),
                    Some('t') => result.push('\t'),
                    Some('u') => {
                        let mut hex = String::new();
                        for _ in 0..4 {
                            match self.chars.next() {
                                Some(h) if h.is_ascii_hexdigit() => hex.push(h),
                                _ => return Err("Invalid unicode escape in JSON string".into()),
                            }
                        }
                        let code = u32::from_str_radix(&hex, 16)
                            .map_err(|e| format!("Invalid hex: {e}"))?;
                        let ch = char::from_u32(code)
                            .ok_or_else(|| "Invalid unicode codepoint".to_string())?;
                        result.push(ch);
                    }
                    _ => return Err("Invalid escape sequence in JSON string".into()),
                },
                other => result.push(other),
            }
        }
        Err("Unterminated JSON string".into())
    }

    fn parse_number(&mut self) -> Result<Value, String> {
        let mut num_str = String::new();
        let mut is_float = false;

        if let Some(&'-') = self.chars.peek() {
            num_str.push(self.chars.next().unwrap());
        }

        while let Some(&c) = self.chars.peek() {
            if c.is_ascii_digit() {
                num_str.push(self.chars.next().unwrap());
            } else if c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-' {
                is_float = true;
                num_str.push(self.chars.next().unwrap());
            } else {
                break;
            }
        }

        if is_float {
            num_str
                .parse::<f64>()
                .map(Value::Float)
                .map_err(|e| format!("Invalid float number `{num_str}`: {e}"))
        } else {
            match num_str.parse::<i64>() {
                Ok(n) => Ok(Value::Int(n)),
                Err(_) => num_str
                    .parse::<f64>()
                    .map(Value::Float)
                    .map_err(|e| format!("Invalid number `{num_str}`: {e}")),
            }
        }
    }

    fn parse_array(&mut self) -> Result<Value, String> {
        if self.chars.next() != Some('[') {
            return Err("Expected `[` at start of array".into());
        }
        let mut items = Vec::new();
        self.skip_whitespace();
        if let Some(&']') = self.chars.peek() {
            self.chars.next();
            return Ok(Value::List(items));
        }

        loop {
            let value = self.parse_value()?;
            items.push(value);
            self.skip_whitespace();
            match self.chars.next() {
                Some(']') => return Ok(Value::List(items)),
                Some(',') => self.skip_whitespace(),
                Some(other) => return Err(format!("Expected `,` or `]`, found `{other}`")),
                None => return Err("Unterminated JSON array".into()),
            }
        }
    }

    fn parse_object(&mut self) -> Result<Value, String> {
        if self.chars.next() != Some('{') {
            return Err("Expected `{` at start of object".into());
        }
        let mut entries = BTreeMap::new();
        self.skip_whitespace();
        if let Some(&'}') = self.chars.peek() {
            self.chars.next();
            return Ok(Value::Map(entries));
        }

        loop {
            self.skip_whitespace();
            let key = self.parse_string()?;
            self.skip_whitespace();
            if self.chars.next() != Some(':') {
                return Err("Expected `:` after object key".into());
            }
            let value = self.parse_value()?;
            entries.insert(key, value);
            self.skip_whitespace();
            match self.chars.next() {
                Some('}') => return Ok(Value::Map(entries)),
                Some(',') => self.skip_whitespace(),
                Some(other) => return Err(format!("Expected `,` or `}}`, found `{other}`")),
                None => return Err("Unterminated JSON object".into()),
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_primitives_and_collections() {
        let json_str = r#"{"name": "Sprout", "count": 42, "ratio": 3.14, "active": true, "extra": null, "tags": ["lang", "studio"]}"#;
        let value = parse(json_str).expect("Valid JSON");
        if let Value::Map(map) = &value {
            assert_eq!(map.get("name"), Some(&Value::String("Sprout".into())));
            assert_eq!(map.get("count"), Some(&Value::Int(42)));
            assert_eq!(map.get("active"), Some(&Value::Bool(true)));
            assert_eq!(map.get("extra"), Some(&Value::Nil));
            if let Some(Value::List(tags)) = map.get("tags") {
                assert_eq!(tags.len(), 2);
                assert_eq!(tags[0], Value::String("lang".into()));
                assert_eq!(tags[1], Value::String("studio".into()));
            } else {
                panic!("Expected list for tags");
            }
        } else {
            panic!("Expected map");
        }
    }

    #[test]
    fn stringify_round_trips_primitives() {
        let mut map = BTreeMap::new();
        map.insert("a".into(), Value::Int(1));
        map.insert("b".into(), Value::String("hello\nworld".into()));
        let serialized = stringify(&Value::Map(map));
        let reparsed = parse(&serialized).unwrap();
        if let Value::Map(m) = reparsed {
            assert_eq!(m.get("a"), Some(&Value::Int(1)));
            assert_eq!(m.get("b"), Some(&Value::String("hello\nworld".into())));
        } else {
            panic!("Expected map");
        }
    }
}
