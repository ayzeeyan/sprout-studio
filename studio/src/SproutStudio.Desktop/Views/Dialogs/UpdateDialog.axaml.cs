using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SproutStudio.Services;

namespace SproutStudio.Views.Dialogs;

public sealed partial class UpdateDialog : Window
{
    private readonly UpdateInfo? _updateInfo;
    private readonly IUpdateService? _updateService;
    private CancellationTokenSource? _downloadCancellation;
    private string? _downloadedPath;
    private bool _isDownloading;

    public UpdateDialog()
    {
        InitializeComponent();
    }

    public UpdateDialog(UpdateInfo updateInfo, IUpdateService updateService)
        : this()
    {
        _updateInfo = updateInfo ?? throw new ArgumentNullException(nameof(updateInfo));
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));

        this.FindControl<TextBlock>("VersionHeading")!.Text = $"Sprout Studio {updateInfo.TagName}";
        this.FindControl<TextBlock>("Subheading")!.Text =
            $"A new version is available (v{updateService.CurrentVersion} → {updateInfo.TagName}).";

        string notes = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
            ? "No release notes provided for this version."
            : updateInfo.ReleaseNotes.Trim();
        this.FindControl<TextBlock>("ReleaseNotesText")!.Text = notes;

        if (updateInfo.FileSize > 0)
        {
            double mb = updateInfo.FileSize / (1024.0 * 1024.0);
            this.FindControl<TextBlock>("FileSizeText")!.Text = $"Installer size: {mb:F1} MB";
        }
        else
        {
            this.FindControl<TextBlock>("FileSizeText")!.Text = "Installer: SproutStudio-Setup.exe";
        }
    }

    private async void ActionButton_Click(object? sender, RoutedEventArgs args)
    {
        if (_updateInfo is null || _updateService is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_downloadedPath) && File.Exists(_downloadedPath))
        {
            try
            {
                _updateService.InstallAndRelaunch(_downloadedPath, _updateInfo.ExpectedSha256);
            }
            catch (Exception ex)
            {
                this.FindControl<TextBlock>("ProgressStatus")!.Text = $"Failed to launch installer: {ex.Message}";
            }
            return;
        }

        if (_isDownloading)
        {
            return;
        }

        _isDownloading = true;
        Button actionBtn = this.FindControl<Button>("ActionButton")!;
        Button cancelBtn = this.FindControl<Button>("CancelButton")!;
        StackPanel progressPanel = this.FindControl<StackPanel>("ProgressPanel")!;
        ProgressBar progressBar = this.FindControl<ProgressBar>("DownloadProgressBar")!;
        TextBlock progressStatus = this.FindControl<TextBlock>("ProgressStatus")!;
        TextBlock progressPercent = this.FindControl<TextBlock>("ProgressPercentage")!;

        actionBtn.IsEnabled = false;
        actionBtn.Content = "Downloading...";
        cancelBtn.Content = "Cancel";
        progressPanel.IsVisible = true;
        progressBar.Value = 0;
        progressStatus.Text = "Connecting to GitHub...";
        progressPercent.Text = "0%";

        _downloadCancellation = new CancellationTokenSource();
        string tempPath = Path.Combine(Path.GetTempPath(), _updateInfo.FileName);

        var progress = new Progress<double>(percent =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                progressBar.Value = percent;
                progressPercent.Text = $"{percent:F0}%";
                progressStatus.Text = $"Downloading {percent:F0}% of {_updateInfo.FileName}...";
            });
        });

        try
        {
            await _updateService.DownloadUpdateAsync(
                _updateInfo.DownloadUrl,
                tempPath,
                _updateInfo.ChecksumsUrl,
                _updateInfo.ExpectedSha256,
                progress,
                _downloadCancellation.Token);

            _downloadedPath = tempPath;
            progressStatus.Text = "Download verified (SHA-256). Launching installer...";
            actionBtn.Content = "Install & Restart";
            actionBtn.IsEnabled = true;

            // Small delay to allow UI to show completion before handoff
            await Task.Delay(300);
            _updateService.InstallAndRelaunch(_downloadedPath, _updateInfo.ExpectedSha256);
        }
        catch (OperationCanceledException)
        {
            _isDownloading = false;
            progressStatus.Text = "Download cancelled.";
            actionBtn.Content = "Download & Install";
            actionBtn.IsEnabled = true;
            cancelBtn.Content = "Remind Me Later";
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            progressStatus.Text = $"Download failed: {ex.Message}";
            actionBtn.Content = "Retry Download";
            actionBtn.IsEnabled = true;
            cancelBtn.Content = "Remind Me Later";
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs args)
    {
        if (_isDownloading && _downloadCancellation is not null)
        {
            _downloadCancellation.Cancel();
        }
        Close(false);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_isDownloading && _downloadCancellation is not null)
        {
            _downloadCancellation.Cancel();
        }
        base.OnClosing(e);
    }
}
