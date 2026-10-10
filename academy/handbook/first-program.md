# Your First Sprout Program

A program is a list of instructions the computer follows from top to bottom. Let's write one.

## Printing

`print` shows a value in the **Console**. Text goes inside double quotes:

```sprout
print("Hello, Sprout!")
```

Press **Run** (or `F5`) and the message appears in the Console panel.

## Values have names

Programs remember things in **variables**. In Guided Sprout you create one with `has`:

```sprout
has name = "Ada"
has age = 36
print(name)
print(age)
```

Read `has name = "Ada"` as "the program *has* a name, and it is Ada".

## Changing a value

Once a variable exists, give it a new value with `=` alone (no `has`):

```sprout
has score = 0
score = score + 10
score = score + 5
print(score)
```

The right side is worked out first (`0 + 10`), then stored back into `score`.

## Joining text

`+` joins two pieces of text:

```sprout
has greeting = "Hello"
has who = "Sprout"
print(greeting + ", " + who + "!")
```

## Notes for humans

A line starting with `#` is a **comment**. Sprout ignores it, so it's perfect for leaving yourself notes:

```sprout
# This is a comment
print("Comments do not run")
```

## Kinds of values

| Kind | Example | Used for |
| :--- | :--- | :--- |
| Int | `42` | whole numbers |
| Float | `3.5` | decimals |
| String | `"hi"` | text |
| Bool | `true`, `false` | yes/no answers |
| Nil | `nil` | "nothing here yet" |

## Try it

Make a variable called `plant`, give it your favorite plant's name, and print `"I am growing "` joined with it.
