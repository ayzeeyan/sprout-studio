# Classes: Grouping Related Actions

As programs grow, related actions belong together. A **class** is a blueprint that groups them, and an **object** (or *instance*) is one thing built from that blueprint.

## Defining a class

```sprout
class Gardener
    action greet(name)
        print("Hello " + name + ", welcome to the garden!")
    end

    action water(plant)
        print("Watering the " + plant)
    end
end
```

## Making and using an object

Call the class like an action to build an object, then use a dot to reach its actions:

```sprout
class Gardener
    action greet(name)
        print("Hello " + name)
    end
end

has gardener = Gardener()
gardener.greet("Sprout")
```

`gardener.greet(...)` reads naturally: "ask the gardener to greet".

## Actions can still return values

```sprout
class Calculator
    action add(a, b)
        return a + b
    end
end

has calc = Calculator()
print(calc.add(2, 3))
```

## What Sprout keeps simple (for now)

Sprout classes currently hold **actions only** and are created with no arguments, like `Gardener()`. Stored fields and custom constructors are on the roadmap. The Java and Python tracks explore fields, constructors, and inheritance in depth.

## Big ideas behind classes

- **Encapsulation**: keep related behavior together behind one name.
- **Abstraction**: callers use `gardener.water("rose")` without caring how it works inside.

## Try it

Make a class `Weather` with an action `report(temperature)` that prints `"Warm"` above 20 and `"Cool"` otherwise. Create one and call it twice.
