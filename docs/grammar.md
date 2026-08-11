# Guided Sprout Grammar (Phase 1)

This document describes only syntax implemented by the current parser. New lines terminate simple statements and delimit block headers. Blank lines and comments beginning with `#` are ignored.

```ebnf
program        = { newline | statement }, EOF ;
statement      = variable | assignment | expression_stmt
               | if_stmt | repeat_stmt | while_stmt
               | action | return_stmt ;

variable       = "has", identifier, "=", expression, line_end ;
assignment     = identifier, "=", expression, line_end ;
expression_stmt= expression, line_end ;
return_stmt    = "return", [ expression ], line_end ;

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
call           = primary, { "(", [ arguments ], ")" } ;
arguments      = expression, { ",", expression } ;
primary        = integer | float | string | "true" | "false" | "nil"
               | identifier | "(", expression, ")" ;
line_end       = newline | EOF ;
```

Strings use double quotes and support `\\n`, `\\t`, `\\"`, and `\\\\`. Numeric literals are base-10 integers or decimal floats. Current values are `Int`, `Float`, `Bool`, `String`, `Nil`, and `Function`; lists, maps, and objects are deferred.

