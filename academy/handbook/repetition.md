# Repeating Work: repeat and while

Copying the same line five times works, but it's fragile: if the count changes, you have to edit five places. **Loops** say "do this again" once.

## Repeat a known number of times

When you know how many times, use `repeat … times`:

```sprout
repeat 3 times
    print("Sprout")
end
```

The count can be any expression, including a variable:

```sprout
has rows = 4
repeat rows times
    print("*****")
end
```

## Keeping a running total

Loops pair nicely with a variable that changes each time around:

```sprout
has total = 0
repeat 5 times
    total = total + 2
end
print(total)
```

## Repeat while something is true

When you don't know the count in advance, use `while`. It checks the condition before every round:

```sprout
has height = 1
while height < 20
    height = height * 2
end
print(height)
```

This keeps doubling until the plant is at least 20 tall, then prints `32`.

> Careful: a `while` loop must eventually make its condition false. If `height` never changed, the loop would run forever.

## Which loop should I use?

- Use **`repeat`** when the number of rounds is known: "do this 3 times".
- Use **`while`** when you stop because of a condition: "keep going until it's tall enough".

## Try it

Use a loop to print a countdown from 5 to 1. Hint: start a variable at 5 and subtract 1 each time around.
