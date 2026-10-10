# Three Ways to Write Sprout

Sprout is one language with three **syntax profiles**. They all mean exactly the same thing to the computer; they just look different, so you can grow from beginner-friendly words toward the style of professional languages.

## The same program, three ways

**Guided** reads like plain English and ends blocks with `end`:

```sprout
has score = 0

action addScore(amount)
    score = score + amount
end

repeat 3 times
    addScore(1)
end

print(score)
```

**Structured** looks like Java, C, or C#: types, semicolons, and braces.

```sprout structured
int score = 0;

void addScore(int amount) {
    score = score + amount;
}

repeat (3) {
    addScore(1);
}

print(score);
```

**Advanced** is short and modern, like Python or Kotlin: `:=` creates a variable and `=>` opens an indented block.

```sprout advanced
score := 0

addScore(amount: Int) =>
    score += amount

repeat 3 =>
    addScore(1)

print(score)
```

## Which should I use?

- **Starting out?** Guided. The words explain themselves.
- **Heading toward Java, C#, or C++?** Structured builds the habits those languages expect.
- **Comfortable already?** Advanced is the quickest to type.

## Switching profiles

Pick a profile from the **Syntax** box in the toolbar. Projects remember their profile in `sprout.toml`, so a whole project always uses one style.

## Try it

Rewrite your countdown loop from the repetition guide in Structured style, switch the profile, and run it.
