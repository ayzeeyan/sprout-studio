# Reading Errors and Debugging

Every programmer writes code with mistakes. The skill is finding them quickly. Studio gives you three tools for that.

## 1. Problems: errors before you run

Studio checks your code as you type. Mistakes get a red squiggle in the editor and an entry in the **Problems** panel (`Ctrl+Shift+M`). Each problem has:

- a **code** such as `SPT101`, useful for searching the docs,
- a **message** saying what's wrong,
- the **line and column** where it happened. Select it to jump there.

The status bar counts errors and warnings, so you always know if something is wrong.

## 2. The Console: what actually happened

**Run** (`F5`) executes your program and shows its output in the **Console**. If the result surprises you, add temporary `print` lines to see values along the way:

```sprout
has total = 0
repeat 3 times
    total = total + 5
    print(total)
end
```

Seeing `5`, `10`, `15` confirms the loop works. Remove the extra prints when you're done.

## 3. The debugger: rewind time

**Debug** (`F6`) records a snapshot after every statement. In the Inspector's **Debug** and **Variables** tabs you can step backward and forward through the run and see each variable's value at that moment. It's like a replay of your program.

## A calm debugging routine

1. **Read the message slowly.** It usually names the exact problem.
2. **Look at the line** it points to, and the line just before it.
3. **Check the simple things**: a missing `end`, a misspelled name, text without quotes.
4. **Print or debug** to compare what you expected with what happened.
5. **Change one thing at a time**, then run again.

## Try it

Introduce a typo on purpose, like `pirnt("hi")`. Read the message in Problems, then fix it.
