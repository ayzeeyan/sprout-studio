using SproutStudio.ViewModels;

namespace SproutStudio.Views.Modes;

public interface IStudioModeView
{
    Task ActivateAsync(MainWindowViewModel workspace, CancellationToken cancellationToken = default);
}
