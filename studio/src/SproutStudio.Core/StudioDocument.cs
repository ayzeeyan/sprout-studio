namespace SproutStudio.Core;

public sealed record StudioDocument(
    string DisplayName,
    string? FilePath,
    string Source,
    SyntaxProfile Profile)
{
    public static StudioDocument Untitled(SyntaxProfile profile = SyntaxProfile.Guided) =>
        new("Untitled.spr", null, SampleSource(profile), profile);

    public StudioDocument WithSource(string source) => this with { Source = source };

    public StudioDocument WithProfile(SyntaxProfile profile) => this with { Profile = profile };

    private static string SampleSource(SyntaxProfile profile) => profile switch
    {
        SyntaxProfile.Guided => "has name = \"Sprout\"\nprint(\"Hello \" + name)\n",
        SyntaxProfile.Structured => "string name = \"Sprout\";\nprint(\"Hello \" + name);\n",
        SyntaxProfile.Advanced => "name := \"Sprout\"\nprint(\"Hello \" + name)\n",
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };
}

