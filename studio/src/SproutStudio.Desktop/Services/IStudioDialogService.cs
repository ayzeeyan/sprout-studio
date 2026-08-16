using SproutStudio.Core;
using SproutStudio.Models;

namespace SproutStudio.Services;

public interface IStudioDialogService
{
    Task<string?> PickDocumentToOpenAsync(CancellationToken cancellationToken = default);

    Task<string?> PickProjectToOpenAsync(CancellationToken cancellationToken = default);

    Task<string?> PickDocumentSavePathAsync(
        string suggestedName,
        CancellationToken cancellationToken = default);

    Task<ProjectCreationRequest?> PromptCreateProjectAsync(
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(
        string title,
        string message,
        string acceptLabel,
        CancellationToken cancellationToken = default);

    Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default);

    Task<string?> ShowCommandPaletteAsync(
        IReadOnlyList<CommandPaletteItem> commands,
        CancellationToken cancellationToken = default);

    Task ShowAcademyAsync(CancellationToken cancellationToken = default);

    Task ShowToolchainsAsync(CancellationToken cancellationToken = default);

    Task ShowDocumentationAsync(CancellationToken cancellationToken = default);

    Task<ProjectInfo?> ShowPackagesAsync(
        ProjectInfo project,
        CancellationToken cancellationToken = default);

    Task<StudioSettingsState?> ShowSettingsAsync(
        StudioSettingsState current,
        CancellationToken cancellationToken = default);

    Task ShowAboutAsync(
        AboutSystemInfo systemInfo,
        CancellationToken cancellationToken = default);
}
