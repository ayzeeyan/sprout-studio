# Syntax Profiles

Sprout has one canonical language with three source profiles. Each frontend tokenizes and parses its own grammar directly into the same AST; profiles are not implemented by rewriting source text.

Select a profile explicitly:

```powershell
sprout run --profile guided examples/hello.spr
sprout run --profile structured examples/structured.spr
sprout run --profile advanced examples/advanced.spr
```

`guided` remains the CLI default.

## Guided

Guided uses descriptive declarations and `end`-delimited blocks:

```text
has score = 0

action addScore(amount)
    score = score + amount
end

repeat 3 times
    addScore(1)
end
```

## Structured

Structured uses surface type names, semicolons, parentheses, and braces:

```text
int score = 0;

void addScore(int amount) {
    score = score + amount;
}

repeat (3) {
    addScore(1);
}
```

Sprout currently accepts type names syntactically and normalizes declarations into the canonical inferred-type AST. Enforcing explicit surface annotations is deferred until canonical type annotations are introduced.

## Advanced

Advanced uses inferred declarations, optional parameter type labels, compound addition, and indentation-sensitive arrow blocks:

```text
score := 0

addScore(amount: Int) =>
    score += amount

repeat 3 =>
    addScore(1)
```

Indentation must align with an enclosing block. Tabs count as one column; spaces are recommended.

## Shared constructs

All profiles support the implemented values, expressions, conditions, loops, actions, returns, zero-argument classes, bound methods, member access, indexing, and explicit Python imports. Structured statements retain their required semicolon:

```text
import python "math" as math;
print(math.sqrt(81));
```

Guided and Advanced use the same explicit import without the semicolon. Profile conversion/formatting is deferred; the AST equivalence API provides its architectural foundation.
