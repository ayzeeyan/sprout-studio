# Making Decisions with if

Programs become interesting when they react differently to different situations. `if` runs a block of code only when a condition is true.

## The shape of an if

```sprout
has temperature = 30
if temperature > 25
    print("It's warm. Water the plants!")
end
```

Everything between the `if` line and `end` is the **block**. Indent it with four spaces so it's easy to see what belongs inside.

## Otherwise: else

`else` gives the "no" answer its own block:

```sprout
has hour = 9
if hour < 12
    print("Good morning")
else
    print("Good afternoon")
end
```

## Asking questions

Conditions compare values and produce `true` or `false`:

| Operator | Meaning |
| :--- | :--- |
| `==` | equal to |
| `!=` | not equal to |
| `<` `<=` | less than (or equal) |
| `>` `>=` | greater than (or equal) |

## Combining questions

Use `and`, `or`, and `not` to build bigger questions:

```sprout
has sunny = true
has weekend = false
if sunny and not weekend
    print("Sunny workday: take a walk at lunch")
end
if sunny or weekend
    print("Good day to garden")
end
```

- `a and b` is true only when **both** are true.
- `a or b` is true when **at least one** is true.
- `not a` flips true and false.

## Checking several cases

Put an `if` inside an `else` to check cases in order:

```sprout
has score = 72
if score >= 90
    print("Excellent")
else
    if score >= 60
        print("Passed")
    else
        print("Keep practicing")
    end
end
```

## Try it

Write a program with a variable `water` (a number of liters). Print `"Too dry"` below 2, `"Just right"` from 2 to 5, and `"Too wet"` above 5.
