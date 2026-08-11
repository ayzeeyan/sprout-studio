using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SproutStudio.Academy;
using SproutStudio.Toolchains;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SproutStudio;

internal sealed class AcademyPrerequisitePanel
{
    private readonly ToolchainManager _toolchains;
    private readonly nint _windowHandle;
    private readonly CancellationToken _cancellationToken;
    private readonly TextBlock _title = new()
    {
        FontSize = 18,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
    };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _components = new()
    {
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Opacity = 0.75,
    };
    private readonly TextBlock _instructions = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Visibility = Visibility.Collapsed,
        Opacity = 0.78,
    };
    private readonly Button _autoDetect = new() { Content = "Auto Detect" };
    private readonly Button _locate = new() { Content = "Locate Installation" };
    private readonly Button _setup = new() { Content = "Setup Instructions" };
    private readonly Button _tryAgain = new() { Content = "Try Again" };
    private readonly Button _cancel = new() { Content = "Cancel" };
    private TrackDefinition? _track;
    private Func<Task>? _retry;

    public AcademyPrerequisitePanel(
        ToolchainManager toolchains,
        nint windowHandle,
        CancellationToken cancellationToken)
    {
        _toolchains = toolchains ?? throw new ArgumentNullException(nameof(toolchains));
        _windowHandle = windowHandle;
        _cancellationToken = cancellationToken;

        var content = new StackPanel { Spacing = 9 };
        content.Children.Add(_title);
        content.Children.Add(new TextBlock
        {
            Text = "Sprout Studio could not locate a working external toolchain for this track. If it is installed elsewhere, choose its installation folder.",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(_status);
        content.Children.Add(_components);
        content.Children.Add(_instructions);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        buttons.Children.Add(_autoDetect);
        buttons.Children.Add(_locate);
        buttons.Children.Add(_setup);
        buttons.Children.Add(_tryAgain);
        buttons.Children.Add(_cancel);
        content.Children.Add(buttons);
        Root = new Border
        {
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 205, 145, 45)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 10, 0, 0),
            Child = content,
            Visibility = Visibility.Collapsed,
        };

        _autoDetect.Click += AutoDetect_Click;
        _locate.Click += Locate_Click;
        _setup.Click += (_, _) => _instructions.Visibility =
            _instructions.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        _tryAgain.Click += TryAgain_Click;
        _cancel.Click += (_, _) => Hide();
    }

    public Border Root { get; }

    public void Show(
        TrackDefinition track,
        ToolchainDetectionResult detection,
        Func<Task> retry)
    {
        _track = track ?? throw new ArgumentNullException(nameof(track));
        _retry = retry ?? throw new ArgumentNullException(nameof(retry));
        _title.Text = $"{detection.DisplayName} Toolchain Required";
        _instructions.Text = track.RequiredToolchainId switch
        {
            "rust" => "Setup: install a supported Rust toolchain with rustc, cargo, and rustfmt. Then return here and choose Auto Detect or Locate Installation. Sprout itself does not require this external toolchain.",
            "go" => "Setup: install a supported Go toolchain with go and gofmt. Then return here and choose Auto Detect or Locate Installation. Sprout itself does not require this external toolchain.",
            _ => "Install the required external toolchain, then choose Auto Detect or Locate Installation.",
        };
        _instructions.Visibility = Visibility.Collapsed;
        Render(detection);
        Root.Visibility = Visibility.Visible;
    }

    public void Hide()
    {
        Root.Visibility = Visibility.Collapsed;
        _track = null;
        _retry = null;
    }

    private async void AutoDetect_Click(object sender, RoutedEventArgs args)
    {
        if (_track?.RequiredToolchainId is not string toolchainId)
        {
            return;
        }
        await RunAsync(async () =>
        {
            Render(await _toolchains.ClearCustomPathAsync(toolchainId, _cancellationToken));
        });
    }

    private async void Locate_Click(object sender, RoutedEventArgs args)
    {
        if (_track?.RequiredToolchainId is not string toolchainId)
        {
            return;
        }
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, _windowHandle);
        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }
        await RunAsync(async () =>
        {
            Render(await _toolchains.ConfigureCustomPathAsync(
                toolchainId,
                folder.Path,
                _cancellationToken));
        });
    }

    private async void TryAgain_Click(object sender, RoutedEventArgs args)
    {
        if (_retry is not null)
        {
            await RunAsync(_retry);
        }
    }

    private async Task RunAsync(Func<Task> operation)
    {
        SetBusy(true);
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
            _status.Foreground = new SolidColorBrush(Colors.IndianRed);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Render(ToolchainDetectionResult detection)
    {
        bool usable = detection.IsUsable;
        _status.Text = usable
            ? $"Environment ready. {detection.Message} Select Try Again to enroll."
            : $"{FormatStatus(detection.Status)} — {detection.Message}";
        _status.Foreground = new SolidColorBrush(usable ? Colors.LightGreen : Colors.IndianRed);
        _components.Text = detection.Components.Count == 0
            ? "No validated components were found."
            : string.Join(
                Environment.NewLine,
                detection.Components.Select(component =>
                    $"{(component.Valid ? "✓" : "✗")} {component.Name}" +
                    (component.Version is null ? string.Empty : $"  {component.Version}")));
    }

    private void SetBusy(bool busy)
    {
        _autoDetect.IsEnabled = !busy;
        _locate.IsEnabled = !busy;
        _setup.IsEnabled = !busy;
        _tryAgain.IsEnabled = !busy;
        _cancel.IsEnabled = !busy;
        if (busy)
        {
            _status.Text = "Validating toolchain…";
            _status.Foreground = new SolidColorBrush(Colors.Gray);
        }
    }

    private static string FormatStatus(ToolchainStatus status) => status switch
    {
        ToolchainStatus.Ready => "READY",
        ToolchainStatus.NotInstalled => "NOT INSTALLED",
        ToolchainStatus.FoundButBroken => "FOUND BUT BROKEN",
        ToolchainStatus.PartialInstallation => "PARTIAL INSTALLATION",
        ToolchainStatus.UnsupportedVersion => "UNSUPPORTED VERSION",
        ToolchainStatus.CustomLocation => "CUSTOM LOCATION",
        _ => status.ToString().ToUpperInvariant(),
    };
}
