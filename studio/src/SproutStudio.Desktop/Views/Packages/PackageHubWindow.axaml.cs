using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Core;
using SproutStudio.Services;

namespace SproutStudio.Views.Packages;

public sealed partial class PackageHubWindow : Window
{
    private IProjectAdvancedClient? _client;
    private readonly ObservableCollection<PackageRow> _packages = [];
    private bool _busy;

    public PackageHubWindow()
    {
        InitializeComponent();
        this.FindControl<ListBox>("PackageList")!.ItemsSource = _packages;
    }

    public PackageHubWindow(IProjectAdvancedClient client, ProjectInfo project)
        : this()
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Project = project ?? throw new ArgumentNullException(nameof(project));
        RefreshRows();
    }

    public ProjectInfo? Project { get; private set; }

    private async void Save_Click(object? sender, RoutedEventArgs args)
    {
        string name = this.FindControl<TextBox>("NameBox")!.Text?.Trim() ?? string.Empty;
        string requirement = this.FindControl<TextBox>("RequirementBox")!.Text?.Trim() ?? string.Empty;
        if (name.Length == 0 || requirement.Length == 0)
        {
            SetStatus("Enter both a package name and a version requirement.");
            return;
        }
        int selected = this.FindControl<ComboBox>("KindBox")!.SelectedIndex;
        await MutateAsync((ProjectDependencyKind)Math.Max(0, selected), name, requirement);
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
        if (_busy || _client is null || Project is null)
        {
            return;
        }
        SetBusy(true);
        try
        {
            ProjectResponse response = await _client.UpdateDependencyAsync(
                Project.RootPath,
                kind,
                name,
                requirement);
            if (!response.Success || response.Project is null)
            {
                SetStatus(string.Join(" ", response.Diagnostics.Select(item => item.Message)));
                return;
            }
            Project = response.Project;
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
        foreach ((string name, string requirement) in Project?.Dependencies
            ?? new Dictionary<string, string>())
        {
            _packages.Add(new PackageRow(ProjectDependencyKind.Sprout, name, requirement));
        }
        foreach ((string name, string requirement) in Project?.PythonDependencies
            ?? new Dictionary<string, string>())
        {
            _packages.Add(new PackageRow(ProjectDependencyKind.Python, name, requirement));
        }
        this.FindControl<TextBlock>("EmptyText")!.IsVisible = _packages.Count == 0;
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

    private void Close_Click(object? sender, RoutedEventArgs args) => Close();
}

public sealed record PackageRow(ProjectDependencyKind Kind, string Name, string Requirement)
{
    public string KindLabel => Kind == ProjectDependencyKind.Python ? "Python" : "Sprout";
}
