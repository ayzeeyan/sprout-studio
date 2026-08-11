using Microsoft.UI.Xaml;

namespace SproutStudio;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string[] arguments = args.Arguments
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Concat(Environment.GetCommandLineArgs().Skip(1))
            .ToArray();
        bool openToolchains = arguments.Contains(
            "--open-toolchains",
            StringComparer.OrdinalIgnoreCase);
        bool openAcademy = arguments.Contains(
            "--open-academy",
            StringComparer.OrdinalIgnoreCase);
        string? academyStatePath = openAcademy
            ? Environment.GetEnvironmentVariable("SPROUT_STUDIO_TEST_ACADEMY_STATE")
            : null;
        _window = new MainWindow(openToolchains, openAcademy, academyStatePath);
        _window.Activate();
    }
}
