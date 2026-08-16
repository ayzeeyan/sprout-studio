using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using SproutStudio.Models;
using SproutStudio.Services;

namespace SproutStudio.Views.About;

public sealed partial class AboutWindow : Window
{
    private readonly AboutSystemInfo _systemInfo;

    public AboutWindow()
        : this(new AboutSystemInfo(
            "Unavailable", "Unavailable", "Unavailable", null,
            "Unavailable", "Unavailable", Environment.Version.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()))
    {
    }

    public AboutWindow(AboutSystemInfo systemInfo)
    {
        _systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
        InitializeComponent();
        StudioVersionText.Text = $"Version {_systemInfo.StudioVersion}";
        StudioBuildText.Text = _systemInfo.StudioBuildConfiguration;
        EngineVersionText.Text = _systemInfo.EngineVersion;
        EngineAbiText.Text = _systemInfo.AbiDisplay;
        EngineArchitectureText.Text = _systemInfo.EngineArchitecture;
        EngineBuildText.Text = _systemInfo.EngineBuildConfiguration;
        RuntimeText.Text = _systemInfo.DotNetRuntime;
        ProcessArchitectureText.Text = _systemInfo.ProcessArchitecture;
    }

    private async void CopySystemInfo_Click(object? sender, RoutedEventArgs args)
    {
        IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            ActionStatus.Text = "Clipboard unavailable";
            return;
        }
        await clipboard.SetTextAsync(_systemInfo.ClipboardText);
        ActionStatus.Text = "Copied";
    }

    private void OpenLogsFolder_Click(object? sender, RoutedEventArgs args)
    {
        Directory.CreateDirectory(StudioLog.LogsDirectoryPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = StudioLog.LogsDirectoryPath,
            UseShellExecute = true,
        });
        ActionStatus.Text = "Opened logs";
    }

    private void Close_Click(object? sender, RoutedEventArgs args) => Close();
}
