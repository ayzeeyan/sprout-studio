using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SproutStudio.Views;

namespace SproutStudio;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string[] arguments = desktop.Args ?? [];
            desktop.MainWindow = new MainWindow(arguments);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
