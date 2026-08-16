using Avalonia;
using SproutStudio.Services;

namespace SproutStudio;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                StudioLog.Error("Unhandled application exception.", exception);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            StudioLog.Error("Unobserved background task exception.", eventArgs.Exception);
            eventArgs.SetObserved();
        };
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
