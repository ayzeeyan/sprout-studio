using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Core;
using SproutStudio.Interop;
using SproutStudio.Services;
using SproutStudio.ViewModels;
using SproutStudio.Views.Modes;

namespace SproutStudio.Views.Packages;

public sealed partial class PackagesModeView : UserControl, IStudioModeView
{
    private readonly IProjectAdvancedClient _client = new NativeSproutEngineClient();
    private readonly ObservableCollection<PackageRow> _packages = [];
    private MainWindowViewModel? _workspace;
    private ProjectInfo? _project;
    private bool _busy;

    public PackagesModeView()
    {
        InitializeComponent();
        this.FindControl<ListBox>("PackageList")!.ItemsSource = _packages;
    }

    public Task ActivateAsync(MainWindowViewModel workspace, CancellationToken cancellationToken = default)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        DataContext = workspace;
        _project = workspace.CurrentProject;
        bool hasProject = _project is not null;
        this.FindControl<StackPanel>("NoProjectPanel")!.IsVisible = !hasProject;
        this.FindControl<Grid>("PackageContent")!.IsVisible = hasProject;
        this.FindControl<Button>("OpenManifestButton")!.IsEnabled = hasProject;
        if (_project is not null)
        {
            this.FindControl<TextBlock>("ProjectText")!.Text = $"{_project.Name} · sprout.toml";
            RefreshRows();
        }
        return Task.CompletedTask;
    }

    private async void Save_Click(object? sender, RoutedEventArgs args)
    {
        string name = this.FindControl<TextBox>("NameBox")!.Text?.Trim() ?? string.Empty;
        string requirement = this.FindControl<TextBox>("RequirementBox")!.Text?.Trim() ?? string.Empty;
        if (name.Length == 0 || requirement.Length == 0)
        {
            SetStatus("Enter both a package name and a version requirement.");
            return;
        }
        await MutateAsync((ProjectDependencyKind)Math.Max(0, this.FindControl<ComboBox>("KindBox")!.SelectedIndex), name, requirement);
    }

    private async void Remove_Click(object? sender, RoutedEventArgs args)
    {
        if (this.FindControl<ListBox>("PackageList")!.SelectedItem is not PackageRow selected)
        {
            SetStatus("Select a package to remove.");
            return;
        }
        await MutateAsync(selected.Kind, selected.Name, null);
    }

    private async Task MutateAsync(ProjectDependencyKind kind, string name, string? requirement)
    {
        if (_busy || _project is null)
        {
            return;
        }
        SetBusy(true);
        try
        {
            ProjectResponse response = await _client.UpdateDependencyAsync(_project.RootPath, kind, name, requirement);
            if (!response.Success || response.Project is null)
            {
                SetStatus(string.Join(" ", response.Diagnostics.Select(diagnostic => diagnostic.Message)));
                return;
            }
            _project = response.Project;
            _workspace?.ApplyProjectUpdate(_project);
            RefreshRows();
            SetStatus(requirement is null
                ? $"Removed {name} from sprout.toml."
                : $"Saved {name} {requirement} in sprout.toml.");
            this.FindControl<TextBox>("NameBox")!.Text = string.Empty;
            this.FindControl<TextBox>("RequirementBox")!.Text = string.Empty;
        }
        catch (Exception exception)
        {
            StudioLog.Error("Package manifest update failed.", exception);
            SetStatus(exception.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshRows()
    {
        _packages.Clear();
        foreach ((string name, string requirement) in _project?.Dependencies ?? new Dictionary<string, string>())
        {
            _packages.Add(new PackageRow(ProjectDependencyKind.Sprout, name, requirement));
        }
        foreach ((string name, string requirement) in _project?.PythonDependencies ?? new Dictionary<string, string>())
        {
            _packages.Add(new PackageRow(ProjectDependencyKind.Python, name, requirement));
        }
        this.FindControl<TextBlock>("PackageEmpty")!.IsVisible = _packages.Count == 0;
        this.FindControl<ListBox>("PackageList")!.IsVisible = _packages.Count > 0;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        this.FindControl<Button>("SaveButton")!.IsEnabled = !busy;
        this.FindControl<Button>("RemoveButton")!.IsEnabled = !busy;
        if (busy)
        {
            SetStatus("Updating the project manifest…");
        }
    }

    private void SetStatus(string message) => this.FindControl<TextBlock>("StatusText")!.Text = message;

    private void OpenProject_Click(object? sender, RoutedEventArgs args) =>
        _workspace?.OpenProjectCommand.Execute(null);

    private async void OpenManifest_Click(object? sender, RoutedEventArgs args)
    {
        if (_project is not null && _workspace is not null)
        {
            await _workspace.OpenDocumentationSourceAsync(Path.Combine(_project.RootPath, "sprout.toml"));
        }
    }
}
