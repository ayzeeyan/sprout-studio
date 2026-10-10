# Actions: Naming Reusable Steps

An **action** is a named group of instructions you can run whenever you need it. Other languages call these *functions* or *methods*.

## Defining and calling

```sprout
action greet()
    print("Hello from an action!")
end

greet()
greet()
```

Defining an action doesn't run it; **calling** it with `greet()` does.

## Inputs: parameters

Parameters let the caller hand values in:

```sprout
action water(plant, liters)
    print("Watering " + plant)
    print(liters)
end

water("fern", 2)
water("cactus", 1)
```

## Outputs: return

`return` sends a value back to whoever called the action:

```sprout
action double(number)
    return number * 2
end

has result = double(21)
print(result)
```

You can use a call anywhere a value fits, even inside another call:

```sprout
action square(n)
    return n * n
end

print(square(3) + square(4))
```

## Why bother?

- **Less repetition**: write the steps once, use them everywhere.
- **Clear names**: `waterGarden()` says what happens without reading every line.
- **Easier fixes**: correct a bug in one place.

## Try it

Write an action `area(width, height)` that returns `width * height`, then print the area of a 3 by 7 garden bed.
