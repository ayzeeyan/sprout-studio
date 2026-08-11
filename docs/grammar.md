# Guided Sprout Grammar

This document describes the Guided frontend. New lines terminate simple statements and delimit block headers. Blank lines and comments beginning with `#` are ignored. See [syntax-profiles.md](syntax-profiles.md) for Structured and Advanced forms.

```ebnf
program        = { newline | statement }, EOF ;
statement      = variable | assignment | expression_stmt
               | if_stmt | repeat_stmt | while_stmt
               | action | class | return_stmt | python_import ;

variable       = "has", identifier, "=", expression, line_end ;
assignment     = identifier, "=", expression, line_end ;
expression_stmt= expression, line_end ;
return_stmt    = "return", [ expression ], line_end ;
python_import  = "import", "python", string, "as", identifier, line_end ;

if_stmt        = "if", expression, newline,
                 block,
                 [ "else", newline, block ],
                 "end", line_end ;
repeat_stmt    = "repeat", expression, "times", newline,
                 block, "end", line_end ;
while_stmt     = "while", expression, newline,
                 block, "end", line_end ;
action         = "action", identifier, "(", [ parameters ], ")", newline,
                 block, "end", line_end ;
class          = "class", identifier, newline,
                 { newline | action },
                 "end", line_end ;
parameters     = identifier, { ",", identifier } ;
block          = { newline | statement } ;

expression     = logic_or ;
logic_or       = logic_and, { "or", logic_and } ;
logic_and      = equality, { "and", equality } ;
equality       = comparison, { ( "==" | "!=" ), comparison } ;
comparison     = term, { ( "<" | "<=" | ">" | ">=" ), term } ;
term           = factor, { ( "+" | "-" ), factor } ;
factor         = unary, { ( "*" | "/" | "%" ), unary } ;
unary          = ( "not" | "-" ), unary | call ;
call           = primary, { "(", [ arguments ], ")" | ".", identifier
               | "[", expression, "]" } ;
arguments      = expression, { ",", expression } ;
primary        = integer | float | string | "true" | "false" | "nil"
               | identifier | "(", expression, ")" ;
line_end       = newline | EOF ;
```

Strings use double quotes and support `\\n`, `\\t`, `\\"`, and `\\\\`. Numeric literals are base-10 integers or decimal floats. Current values are `Int`, `Float`, `Bool`, `String`, `Nil`, `Function`, `Class`, class instances, safely converted Python lists/maps, and opaque Python object proxies. Indexing supports strings, converted lists/maps, and Python proxies. Classes contain actions and use zero-argument construction; fields and custom constructors are deferred.

Python imports are always explicit. The string is a validated dotted Python module name and the alias enters the normal Sprout scope:

```text
import python "json" as json
has values = json.loads("[10, 20]")
print(values[1])
```
