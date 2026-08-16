using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SproutStudio.Services;
using SproutStudio.Toolchains;
using SproutStudio.Views.Dialogs;

namespace SproutStudio.Views.Toolchains;

public sealed partial class ToolchainsWindow : Window
{
    private readonly ToolchainManager _manager;
    private bool _loading;

    public ToolchainsWindow()
        : this(ToolchainManager.CreateDefault())
    {
    }

    public ToolchainsWindow(ToolchainManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        InitializeComponent();
        Opened += async (_, _) => await RefreshAsync();
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs args) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (_loading)
        {
            return;
        }
        _loading = true;
        TextBlock loading = this.FindControl<TextBlock>("LoadingText")!;
        ListBox list = this.FindControl<ListBox>("ToolchainList")!;
        loading.Text = "Detecting installed toolchains…";
        loading.IsVisible = true;
        list.IsVisible = false;
        try
        {
            IReadOnlyList<ToolchainDetectionResult> results = await _manager.DetectAllAsync();
            list.ItemsSource = results;
            list.IsVisible = true;
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
        }
    }

    private async void Browse_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { DataContext: ToolchainDetectionResult detection })
        {
            return;
        }
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = $"Choose {detection.DisplayName} installation",
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
            await new MessageDialog("Toolchain configuration failed", exception.Message, "OK", showCancel: false)
                .ShowDialog<bool>(this);
        }
    }

    private async void Clear_Click(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: ToolchainDetectionResult detection })
        {
            await _manager.ClearCustomPathAsync(detection.Id);
            await RefreshAsync();
        }
    }
}
