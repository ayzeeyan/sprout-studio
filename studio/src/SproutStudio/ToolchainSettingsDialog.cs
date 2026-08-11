using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SproutStudio.Toolchains;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SproutStudio;

internal sealed class ToolchainSettingsDialog
{
    private readonly ToolchainManager _manager;
    private readonly nint _windowHandle;
    private readonly ContentDialog _dialog;
    private readonly Dictionary<string, ToolchainPanel> _panels =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _lifetime = new();

    public ToolchainSettingsDialog(ToolchainManager manager, nint windowHandle)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _windowHandle = windowHandle;
        var content = new StackPanel { Spacing = 12, MinWidth = 580 };
        content.Children.Add(new TextBlock
        {
            Text = "Sprout Studio validates tools by running their version commands. " +
                "External languages are checked only when this page is opened or refreshed.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        });
        foreach (IToolchainAdapter adapter in _manager.RegisteredToolchains
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var panel = new ToolchainPanel(adapter.Id, adapter.DisplayName);
            panel.RefreshRequested += (_, _) => _ = RefreshAsync(panel, _lifetime.Token);
            panel.AutoDetectRequested += (_, _) => _ = AutoDetectAsync(panel, _lifetime.Token);
            panel.ExecutableRequested += (_, _) => _ = SelectExecutableAsync(panel, _lifetime.Token);
            panel.FolderRequested += (_, _) => _ = SelectFolderAsync(panel, _lifetime.Token);
            _panels.Add(adapter.Id, panel);
            content.Children.Add(panel.Root);
        }

        _dialog = new ContentDialog
        {
            Title = "Toolchain settings",
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 620,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            CloseButtonText = "Close",
        };
        _dialog.Opened += Dialog_Opened;
        _dialog.Closed += (_, _) => _lifetime.Cancel();
    }

    public async Task ShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _dialog.XamlRoot = xamlRoot;
        await _dialog.ShowAsync();
    }

    public event EventHandler? Opened;

    private async void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        Opened?.Invoke(this, EventArgs.Empty);
        try
        {
            foreach (ToolchainPanel panel in _panels.Values)
            {
                panel.SetLoading("Detecting and validating…");
            }
            IReadOnlyList<ToolchainDetectionResult> results = await _manager.DetectAllAsync(
                _lifetime.Token);
            foreach (ToolchainDetectionResult result in results)
            {
                _panels[result.Id].Render(result);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            foreach (ToolchainPanel panel in _panels.Values)
            {
                panel.RenderFailure(exception.Message);
            }
        }
    }

    private async Task RefreshAsync(ToolchainPanel panel, CancellationToken cancellationToken)
    {
        panel.SetLoading("Rechecking…");
        try
        {
            panel.Render(await _manager.DetectAsync(panel.Id, cancellationToken: cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            panel.RenderFailure(exception.Message);
        }
    }

    private async Task AutoDetectAsync(ToolchainPanel panel, CancellationToken cancellationToken)
    {
        panel.SetLoading("Searching PATH and common locations…");
        try
        {
            panel.Render(await _manager.ClearCustomPathAsync(panel.Id, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            panel.RenderFailure(exception.Message);
        }
    }

    private async Task SelectExecutableAsync(
        ToolchainPanel panel,
        CancellationToken cancellationToken)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, _windowHandle);
        Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await ConfigureAsync(panel, file.Path, cancellationToken);
        }
    }

    private async Task SelectFolderAsync(
        ToolchainPanel panel,
        CancellationToken cancellationToken)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _windowHandle);
        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            await ConfigureAsync(panel, folder.Path, cancellationToken);
        }
    }

    private async Task ConfigureAsync(
        ToolchainPanel panel,
        string path,
        CancellationToken cancellationToken)
    {
        panel.SetLoading("Validating custom location…");
        try
        {
            panel.Render(await _manager.ConfigureCustomPathAsync(panel.Id, path, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            panel.RenderFailure(exception.Message);
        }
    }

    private sealed class ToolchainPanel
    {
        private readonly TextBlock _status = new() { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        private readonly TextBlock _version = new() { Opacity = 0.78 };
        private readonly TextBlock _path = new()
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        };
        private readonly TextBlock _message = new() { TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _components = new()
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        };
        private readonly Button _refresh = new() { Content = "Recheck" };
        private readonly Button _auto = new() { Content = "Auto detect" };
        private readonly Button _executable = new() { Content = "Locate executable" };
        private readonly Button _folder = new() { Content = "Locate folder" };

        public ToolchainPanel(string id, string displayName)
        {
            Id = id;
            var heading = new Grid { ColumnSpacing = 12 };
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(new TextBlock
            {
                Text = displayName,
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            Grid.SetColumn(_status, 1);
            heading.Children.Add(_status);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            buttons.Children.Add(_refresh);
            buttons.Children.Add(_auto);
            buttons.Children.Add(_executable);
            buttons.Children.Add(_folder);
            _refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
            _auto.Click += (_, _) => AutoDetectRequested?.Invoke(this, EventArgs.Empty);
            _executable.Click += (_, _) => ExecutableRequested?.Invoke(this, EventArgs.Empty);
            _folder.Click += (_, _) => FolderRequested?.Invoke(this, EventArgs.Empty);

            var body = new StackPanel { Spacing = 6 };
            body.Children.Add(heading);
            body.Children.Add(_version);
            body.Children.Add(_path);
            body.Children.Add(_message);
            body.Children.Add(_components);
            body.Children.Add(buttons);
            Root = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.35 },
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                Child = body,
            };
        }

        public string Id { get; }

        public Border Root { get; }

        public event EventHandler? RefreshRequested;

        public event EventHandler? AutoDetectRequested;

        public event EventHandler? ExecutableRequested;

        public event EventHandler? FolderRequested;

        public void SetLoading(string message)
        {
            SetEnabled(false);
            _status.Text = "CHECKING";
            _status.Foreground = new SolidColorBrush(Colors.DodgerBlue);
            _message.Text = message;
        }

        public void Render(ToolchainDetectionResult result)
        {
            SetEnabled(true);
            _status.Text = StatusText(result.Status);
            _status.Foreground = new SolidColorBrush(StatusColor(result.Status));
            _version.Text = result.Version is null ? "Version: unavailable" : $"Version: {result.Version}";
            _path.Text = result.ExecutablePath is null
                ? "Executable: not found"
                : $"Executable: {result.ExecutablePath}";
            _message.Text = result.Message;
            string components = result.Components.Count == 0
                ? "No components discovered."
                : string.Join(
                    "  ·  ",
                    result.Components.Select(component =>
                        $"{(component.Valid ? "✓" : "✕")} {component.Name}"));
            _components.Text = result.Capabilities == ToolchainCapability.None
                ? components
                : $"{components}\nCapabilities: {result.Capabilities}";
        }

        public void RenderFailure(string message)
        {
            SetEnabled(true);
            _status.Text = "CHECK FAILED";
            _status.Foreground = new SolidColorBrush(Colors.IndianRed);
            _message.Text = message;
        }

        private void SetEnabled(bool enabled)
        {
            _refresh.IsEnabled = enabled;
            _auto.IsEnabled = enabled;
            _executable.IsEnabled = enabled;
            _folder.IsEnabled = enabled;
        }

        private static string StatusText(ToolchainStatus status) => status switch
        {
            ToolchainStatus.Ready => "READY",
            ToolchainStatus.NotInstalled => "NOT INSTALLED",
            ToolchainStatus.FoundButBroken => "FOUND BUT BROKEN",
            ToolchainStatus.PartialInstallation => "PARTIAL INSTALLATION",
            ToolchainStatus.UnsupportedVersion => "UNSUPPORTED VERSION",
            ToolchainStatus.CustomLocation => "CUSTOM LOCATION · READY",
            _ => status.ToString().ToUpperInvariant(),
        };

        private static Windows.UI.Color StatusColor(ToolchainStatus status) => status switch
        {
            ToolchainStatus.Ready or ToolchainStatus.CustomLocation => Colors.MediumSeaGreen,
            ToolchainStatus.NotInstalled => Colors.Gray,
            ToolchainStatus.PartialInstallation or ToolchainStatus.UnsupportedVersion => Colors.DarkOrange,
            _ => Colors.IndianRed,
        };
    }
}
