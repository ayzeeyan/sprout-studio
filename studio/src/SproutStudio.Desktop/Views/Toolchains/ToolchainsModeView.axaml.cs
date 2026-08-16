using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SproutStudio.Services;
using SproutStudio.Toolchains;
using SproutStudio.ViewModels;
using SproutStudio.Views.Modes;

namespace SproutStudio.Views.Toolchains;

public sealed partial class ToolchainsModeView : UserControl, IStudioModeView
{
    private readonly ToolchainManager _manager;
    private bool _loading;

    public ToolchainsModeView()
        : this(ToolchainManager.CreateDefault())
    {
    }

    internal ToolchainsModeView(ToolchainManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        InitializeComponent();
    }

    public async Task ActivateAsync(MainWindowViewModel workspace, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        DataContext = workspace;
        if (this.FindControl<ItemsControl>("ToolchainList")!.ItemsSource is null)
        {
            await RefreshAsync(cancellationToken);
        }
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs args) => await RefreshAsync();

    private async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_loading)
        {
            return;
        }
        _loading = true;
        TextBlock loading = this.FindControl<TextBlock>("LoadingText")!;
        ScrollViewer scroll = this.FindControl<ScrollViewer>("ToolchainScroll")!;
        Button refresh = this.FindControl<Button>("RefreshButton")!;
        loading.Text = "Detecting installed toolchains…";
        loading.IsVisible = true;
        scroll.IsVisible = false;
        refresh.IsEnabled = false;
        try
        {
            IReadOnlyList<ToolchainDetectionResult> detections = await _manager.DetectAllAsync(cancellationToken);
            ToolchainModeRow[] rows =
            [
                ToolchainModeRow.BundledSprout(),
                .. detections.Select(ToolchainModeRow.FromDetection),
            ];
            this.FindControl<ItemsControl>("ToolchainList")!.ItemsSource = rows;
            scroll.IsVisible = true;
            loading.IsVisible = false;
        }
        catch (Exception exception)
        {
            StudioLog.Error("Toolchain detection failed.", exception);
            loading.Text = exception.Message;
        }
        finally
        {
            _loading = false;
            refresh.IsEnabled = true;
        }
    }

    private async void Browse_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: ToolchainModeRow { Detection: { } detection } row }
            || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }
        IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Choose {row.DisplayName} installation",
            AllowMultiple = false,
        });
        string? path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        try
        {
            await _manager.ConfigureCustomPathAsync(detection.Id, path);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            StudioLog.Error("Custom toolchain configuration failed.", exception);
            this.FindControl<TextBlock>("LoadingText")!.Text = exception.Message;
            this.FindControl<TextBlock>("LoadingText")!.IsVisible = true;
        }
    }

    private async void Clear_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: ToolchainModeRow { Detection: { } detection } })
        {
            await _manager.ClearCustomPathAsync(detection.Id);
            await RefreshAsync();
        }
    }
}

public sealed record ToolchainModeRow(
    string LanguageKey,
    string DisplayName,
    string StatusLabel,
    string VersionLabel,
    string ExecutablePath,
    string Message,
    bool CanConfigure,
    ToolchainDetectionResult? Detection)
{
    public static ToolchainModeRow BundledSprout() => new(
        "sprout",
        "Sprout",
        "BUNDLED · READY",
        "Included with Sprout Studio",
        AppContext.BaseDirectory,
        "The authoritative Sprout engine and runtime ship with Studio.",
        false,
        null);

    public static ToolchainModeRow FromDetection(ToolchainDetectionResult detection) => new(
        detection.Id,
        detection.DisplayName,
        Status(detection.Status),
        detection.Version ?? "Version unavailable",
        detection.ExecutablePath ?? "No executable selected",
        detection.Message,
        true,
        detection);

    private static string Status(ToolchainStatus status) => status switch
    {
        ToolchainStatus.Ready => "READY",
        ToolchainStatus.CustomLocation => "CUSTOM · READY",
        ToolchainStatus.NotInstalled => "MISSING",
        ToolchainStatus.FoundButBroken => "BROKEN",
        ToolchainStatus.PartialInstallation => "PARTIAL",
        ToolchainStatus.UnsupportedVersion => "UNSUPPORTED",
        _ => status.ToString().ToUpperInvariant(),
    };
}
