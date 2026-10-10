"""Generates the AvaloniaEdit syntax definitions (.xshd) used by Sprout Studio.

Every language shares one dark palette so code looks consistent across the editor, the
Academy workspace, and the Learn inspector. Run from the repository root:

    python tools/generate-syntax-definitions.py
"""

from pathlib import Path
from xml.sax.saxutils import escape

OUTPUT = Path(__file__).resolve().parent.parent / "studio" / "src" / "SproutStudio.Desktop" / "Content" / "Syntax"

PALETTE = {
    "Comment": ('#6F8F74', "italic", None),
    "String": ('#E3B76B', None, None),
    "Number": ('#B6E3A3', None, None),
    "Keyword": ('#C792EA', None, "bold"),
    "Control": ('#E68FD0', None, None),
    "Literal": ('#7DB8F0', None, None),
    "Type": ('#E5C07B', None, None),
    "BuiltIn": ('#65C7C9', None, None),
    "Function": ('#82AAFF', None, None),
    "Attribute": ('#D7A0E8', None, None),
    "Preprocessor": ('#C9A26D', None, None),
    "Heading": ('#4CD484', None, "bold"),
    "Emphasis": ('#E9EFEB', "italic", None),
    "Strong": ('#E9EFEB', None, "bold"),
    "Link": ('#6FB7FF', None, None),
    "Code": ('#E3B76B', None, None),
    "Property": ('#9CDCFE', None, None),
}

NUMBER = r"\b0[xX][0-9a-fA-F_]+\b|\b[0-9][0-9_]*(\.[0-9_]+)?([eE][+-]?[0-9]+)?[a-zA-Z0-9_]*\b"
FUNCTION_CALL = r"\b[A-Za-z_][A-Za-z0-9_]*(?=\s*\()"


def words(*text):
    return [word for chunk in text for word in chunk.split()]


LANGUAGES = {
    "Sprout": dict(
        extensions=".spr",
        line_comments=["#"],
        strings=['"'],
        keywords=words("has action class end import python as and or not guided structured advanced"),
        control=words("if else repeat times while return"),
        literals=words("true false nil"),
        types=words("int float bool text auto void Int Float Bool Text Auto Void String"),
        builtins=words("print len canvas_clear canvas_rect canvas_circle"),
    ),
    "Python": dict(
        extensions=".py;.pyw",
        line_comments=["#"],
        triple_strings=['"""', "'''"],
        strings=['"', "'"],
        keywords=words("def class lambda import from as global nonlocal pass del in is not and or async await with assert"),
        control=words("if elif else for while break continue return yield try except finally raise match case"),
        literals=words("True False None self cls"),
        types=words("int float str bool list dict set tuple bytes object Exception"),
        builtins=words("print len range enumerate zip map filter sorted sum min max abs open input isinstance super type repr"),
        attribute=r"@[A-Za-z_][A-Za-z0-9_.]*",
    ),
    "Java": dict(
        extensions=".java",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"', "'"],
        keywords=words("public private protected static final abstract class interface enum record extends implements new this super package import void synchronized volatile transient native throws instanceof var sealed permits"),
        control=words("if else for while do switch case default break continue return try catch finally throw yield"),
        literals=words("true false null"),
        types=words("int long short byte char boolean double float String Object List Map Set ArrayList HashMap Integer Long Double Boolean Optional"),
        builtins=words("System out println printf"),
        attribute=r"@[A-Za-z_][A-Za-z0-9_.]*",
    ),
    "Rust": dict(
        extensions=".rs",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"'],
        keywords=words("fn let mut const static struct enum trait impl pub use mod crate self Self super where as dyn ref move unsafe extern type async await"),
        control=words("if else match for while loop break continue return in"),
        literals=words("true false None Some Ok Err"),
        types=words("i8 i16 i32 i64 i128 isize u8 u16 u32 u64 u128 usize f32 f64 bool char str String Vec Option Result Box HashMap"),
        builtins=words("println print format vec panic assert assert_eq todo"),
        attribute=r"#!?\[[^\]]*\]",
        macro=r"\b[A-Za-z_][A-Za-z0-9_]*!",
    ),
    "Go": dict(
        extensions=".go",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"', "`"],
        keywords=words("func package import var const type struct interface map chan go defer select"),
        control=words("if else for range switch case default break continue return fallthrough goto"),
        literals=words("true false nil iota"),
        types=words("int int8 int16 int32 int64 uint uint8 uint16 uint32 uint64 float32 float64 string bool byte rune error any"),
        builtins=words("fmt Println Printf Sprintf len cap append make new panic recover delete copy"),
    ),
    "C": dict(
        extensions=".c;.h",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"', "'"],
        keywords=words("struct union enum typedef static extern const volatile sizeof inline register"),
        control=words("if else for while do switch case default break continue return goto"),
        literals=words("NULL true false"),
        types=words("int long short char float double void unsigned signed bool size_t"),
        builtins=words("printf scanf malloc free memcpy strlen"),
        preprocessor=r"^\s*#\s*[a-z]+",
    ),
    "Cpp": dict(
        name="C++",
        extensions=".cc;.cpp;.cxx;.hpp;.hxx",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"', "'"],
        keywords=words("class struct namespace using template typename public private protected virtual override final static const constexpr auto new delete this operator friend explicit inline enum union typedef noexcept"),
        control=words("if else for while do switch case default break continue return try catch throw"),
        literals=words("nullptr true false NULL"),
        types=words("int long short char float double void bool unsigned signed size_t string vector map"),
        builtins=words("std cout cin endl printf"),
        preprocessor=r"^\s*#\s*[a-z]+",
    ),
    "CSharp": dict(
        name="C#",
        extensions=".cs",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"', "'"],
        keywords=words("public private protected internal static readonly const sealed abstract virtual override class struct record interface enum namespace using new this base var async await partial get set init required in out ref params"),
        control=words("if else for foreach while do switch case default break continue return try catch finally throw yield when"),
        literals=words("true false null"),
        types=words("int long short byte char bool double float decimal string object void Task List Dictionary"),
        builtins=words("Console WriteLine nameof typeof"),
        attribute=r"^\s*\[[A-Za-z][^\]]*\]",
        preprocessor=r"^\s*#\s*[a-z]+",
    ),
    "JavaScript": dict(
        extensions=".js;.mjs;.cjs",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"', "'", "`"],
        keywords=words("function const let var class extends new this super import export from as async await typeof instanceof in of delete void static get set"),
        control=words("if else for while do switch case default break continue return try catch finally throw yield"),
        literals=words("true false null undefined NaN Infinity"),
        types=words("Array Object String Number Boolean Map Set Promise Date Error"),
        builtins=words("console log document window require module JSON Math"),
    ),
    "TypeScript": dict(
        extensions=".ts;.tsx",
        line_comments=["//"],
        block_comment=(r"/\*", r"\*/"),
        strings=['"', "'", "`"],
        keywords=words("function const let var class extends implements interface type enum namespace new this super import export from as async await typeof keyof instanceof in of readonly public private protected static abstract declare get set"),
        control=words("if else for while do switch case default break continue return try catch finally throw yield"),
        literals=words("true false null undefined"),
        types=words("string number boolean any unknown never void object Array Record Promise Map Set"),
        builtins=words("console log JSON Math"),
        attribute=r"@[A-Za-z_][A-Za-z0-9_.]*",
    ),
    "Json": dict(
        name="JSON",
        extensions=".json",
        property=r'"(?:[^"\\]|\\.)*"(?=\s*:)',
        strings=['"'],
        literals=words("true false null"),
    ),
    "Toml": dict(
        name="TOML",
        extensions=".toml",
        line_comments=["#"],
        triple_strings=['"""', "'''"],
        strings=['"', "'"],
        literals=words("true false"),
        heading=r"^\s*\[\[?[^\]]+\]\]?",
        property=r"^\s*[A-Za-z0-9_.\-]+(?=\s*=)",
    ),
    "Markdown": dict(
        extensions=".md;.markdown",
        markdown=True,
    ),
}


def color_elements():
    lines = []
    for name, (foreground, style, weight) in PALETTE.items():
        attributes = f'name="{name}" foreground="{foreground}"'
        if style:
            attributes += f' fontStyle="{style}"'
        if weight:
            attributes += f' fontWeight="{weight}"'
        lines.append(f"  <Color {attributes} />")
    return lines


def keyword_block(color, values):
    if not values:
        return []
    body = "".join(f"<Word>{escape(word)}</Word>" for word in values)
    return [f'    <Keywords color="{color}">{body}</Keywords>']


def rule_pattern(pattern):
    # AvaloniaEdit compiles <Rule> regexes with IgnorePatternWhitespace, where an unescaped '#'
    # starts a comment and literal spaces vanish. Escape both so patterns mean what they say.
    return pattern.replace("#", "\\#").replace(" ", "\\x20")


def rule(color, pattern):
    return [f'    <Rule color="{color}">{escape(rule_pattern(pattern))}</Rule>']


def string_span(delimiter, multiline=False):
    quote = escape(delimiter, {'"': "&quot;"})
    flag = ' multiline="true"' if multiline else ""
    escapes = '' if delimiter == "`" else '<RuleSet><Span begin="\\\\" end="." /></RuleSet>'
    return [f'    <Span color="String"{flag} begin="{quote}" end="{quote}">{escapes}</Span>']


def markdown_rules():
    return [
        '    <Rule color="Heading">^\\s{0,3}\\#{1,6}\\s.*$</Rule>',
        '    <Span color="Code" multiline="true" begin="^\\s*```" end="^\\s*```" />',
        '    <Rule color="Code">`[^`\\n]+`</Rule>',
        '    <Rule color="Strong">\\*\\*[^*\\n]+\\*\\*</Rule>',
        '    <Rule color="Emphasis">(?&lt;![*\\w])\\*[^*\\n]+\\*(?![*\\w])</Rule>',
        '    <Rule color="Link">\\[[^\\]\\n]*\\]\\([^)\\n]*\\)</Rule>',
        '    <Rule color="Keyword">^\\s*([-*+]|\\d+\\.)\\s</Rule>',
        '    <Rule color="Comment">^\\s*&gt;.*$</Rule>',
    ]


def definition(key, spec):
    name = spec.get("name", key)
    lines = [
        '<?xml version="1.0"?>',
        "<!-- Generated by tools/generate-syntax-definitions.py; edit the generator, not this file. -->",
        f'<SyntaxDefinition name="{escape(name)}" extensions="{spec["extensions"]}" '
        'xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">',
        *color_elements(),
        "  <RuleSet>",
    ]
    if spec.get("markdown"):
        lines += markdown_rules()
    else:
        for comment in spec.get("line_comments", []):
            lines.append(f'    <Span color="Comment" begin="{escape(comment)}" />')
        if "block_comment" in spec:
            begin, end = spec["block_comment"]
            lines.append(f'    <Span color="Comment" multiline="true" begin="{begin}" end="{end}" />')
        if "attribute" in spec:
            lines += rule("Attribute", spec["attribute"])
        if "preprocessor" in spec:
            lines += rule("Preprocessor", spec["preprocessor"])
        if "heading" in spec:
            lines += rule("Heading", spec["heading"])
        if "property" in spec:
            lines += rule("Property", spec["property"])
        for delimiter in spec.get("triple_strings", []):
            lines += string_span(delimiter, multiline=True)
        for delimiter in spec.get("strings", []):
            lines += string_span(delimiter, multiline=delimiter == "`")
        lines += keyword_block("Keyword", spec.get("keywords"))
        lines += keyword_block("Control", spec.get("control"))
        lines += keyword_block("Literal", spec.get("literals"))
        lines += keyword_block("Type", spec.get("types"))
        lines += keyword_block("BuiltIn", spec.get("builtins"))
        if "macro" in spec:
            lines += rule("BuiltIn", spec["macro"])
        lines += rule("Number", NUMBER)
        if key not in ("Json", "Toml"):
            lines += rule("Function", FUNCTION_CALL)
    lines += ["  </RuleSet>", "</SyntaxDefinition>", ""]
    return "\n".join(lines)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for key, spec in LANGUAGES.items():
        (OUTPUT / f"{key}.xshd").write_text(definition(key, spec), encoding="utf-8", newline="\n")
    print(f"Wrote {len(LANGUAGES)} syntax definitions to {OUTPUT}")


if __name__ == "__main__":
    main()
