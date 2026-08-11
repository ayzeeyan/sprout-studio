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
        bool openToolchains = args.Arguments
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Concat(Environment.GetCommandLineArgs().Skip(1))
            .Contains("--open-toolchains", StringComparer.OrdinalIgnoreCase);
        _window = new MainWindow(openToolchains);
        _window.Activate();
    }
}
