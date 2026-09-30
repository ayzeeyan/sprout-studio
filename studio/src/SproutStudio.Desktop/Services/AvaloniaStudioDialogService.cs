using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SproutStudio.Core;
using SproutStudio.Interop;
using SproutStudio.Models;
using SproutStudio.Toolchains;
using SproutStudio.Views.Dialogs;
using SproutStudio.Views.Documentation;
using SproutStudio.Views.Packages;
using SproutStudio.Views.Settings;
using SproutStudio.Views.Toolchains;
using SproutStudio.Views.About;

namespace SproutStudio.Services;

public sealed class AvaloniaStudioDialogService : IStudioDialogService
{
    private readonly Window _owner;
    private readonly ToolchainManager _toolchains;

    public AvaloniaStudioDialogService(Window owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _toolchains = ToolchainManager.CreateDefault();
    }

    public async Task<string?> PickDocumentToOpenAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IStorageFile> files = await _owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open in Sprout Studio",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Sprout and source files")
                    {
                        Patterns = ["*.spr", "*.rs", "*.go", "*.py", "*.java", "*.js", "*.ts", "*.json", "*.toml", "*.md", "*.txt"],
                    },
                    FilePickerFileTypes.All,
                ],
            });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickProjectToOpenAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IStorageFolder> folders = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Open Sprout Project",
                AllowMultiple = false,
            });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickDocumentSavePathAsync(
        string suggestedName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IStorageFile? file = await _owner.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Save Sprout Source",
                SuggestedFileName = suggestedName,
                DefaultExtension = "spr",
                FileTypeChoices =
                [
                    new FilePickerFileType("Sprout source") { Patterns = ["*.spr"] },
                ],
            });
        return file?.TryGetLocalPath();
    }

    public async Task<ProjectCreationRequest?> PromptCreateProjectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IStorageFolder> folders = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Choose the folder that will contain the new project",
                AllowMultiple = false,
            });
        string? parent = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(parent))
        {
            return null;
        }
        return await new NewProjectDialog(parent).ShowDialog<ProjectCreationRequest?>(_owner);
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string acceptLabel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new MessageDialog(title, message, acceptLabel).ShowDialog<bool>(_owner);
    }

    public async Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await new MessageDialog(title, message, "OK", showCancel: false).ShowDialog<bool>(_owner);
    }

    public Task<string?> ShowCommandPaletteAsync(
        IReadOnlyList<CommandPaletteItem> commands,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new CommandPaletteDialog(commands).ShowDialog<string?>(_owner);
    }

    public async Task ShowToolchainsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await new ToolchainsWindow(_toolchains).ShowDialog(_owner);
    }

    public async Task ShowDocumentationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await new DocumentationWindow().ShowDialog(_owner);
    }

    public async Task<ProjectInfo?> ShowPackagesAsync(
        ProjectInfo project,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = new PackageHubWindow(new NativeSproutEngineClient(), project);
        await window.ShowDialog(_owner);
        return window.Project;
    }

    public Task<StudioSettingsState?> ShowSettingsAsync(
        StudioSettingsState current,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new SettingsWindow(current).ShowDialog<StudioSettingsState?>(_owner);
    }

    public async Task ShowAboutAsync(
        AboutSystemInfo systemInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(systemInfo);
        cancellationToken.ThrowIfCancellationRequested();
        await new AboutWindow(systemInfo).ShowDialog(_owner);
    }

    public async Task ShowUpdateDialogAsync(
        UpdateInfo updateInfo,
        IUpdateService updateService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateInfo);
        ArgumentNullException.ThrowIfNull(updateService);
        cancellationToken.ThrowIfCancellationRequested();
        var dialog = new UpdateDialog(updateInfo, updateService);
        await dialog.ShowDialog(_owner);
    }
}
