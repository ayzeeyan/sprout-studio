using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SproutStudio.Core;

namespace SproutStudio;

internal sealed class PackageHubDialog
{
    private readonly IProjectAdvancedClient _client;
    private readonly ContentDialog _dialog;
    private readonly ObservableCollection<PackageRow> _packages = [];
    private readonly ListView _packageList = new()
    {
        SelectionMode = ListViewSelectionMode.Single,
        MinHeight = 280,
    };
    private readonly ComboBox _kind = new()
    {
        Header = "Dependency kind",
        ItemsSource = new[] { "Sprout", "Python" },
        SelectedIndex = 1,
        Width = 150,
    };
    private readonly TextBox _name = new()
    {
        Header = "Package name",
        PlaceholderText = "pygame",
        Width = 220,
    };
    private readonly TextBox _requirement = new()
    {
        Header = "Version requirement",
        PlaceholderText = "2.6.* or >=2.0",
        Width = 220,
    };
    private readonly Button _addButton = new() { Content = "Add or update" };
    private readonly Button _removeButton = new() { Content = "Remove selected" };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Opacity = 0.72 };
    private ProjectInfo _project;

    public PackageHubDialog(IProjectAdvancedClient client, ProjectInfo project)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _project = project ?? throw new ArgumentNullException(nameof(project));

        _packageList.ItemsSource = _packages;
        _packageList.ItemTemplate = BuildItemTemplate();
        var fields = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        fields.Children.Add(_kind);
        fields.Children.Add(_name);
        fields.Children.Add(_requirement);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(_addButton);
        actions.Children.Add(_removeButton);
        var content = new StackPanel { MinWidth = 760, Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "Manage validated entries in sprout.toml. Python packages are isolated under .sprout/python when synchronized with `sprout python sync`.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        });
        content.Children.Add(_packageList);
        content.Children.Add(fields);
        content.Children.Add(actions);
        content.Children.Add(_status);
        _dialog = new ContentDialog
        {
            Title = "Package Hub",
            Content = content,
            CloseButtonText = "Close",
        };
        _dialog.Resources["ContentDialogMaxWidth"] = 840d;
        _addButton.Click += AddButton_Click;
        _removeButton.Click += RemoveButton_Click;
        RefreshRows();
    }

    public ProjectInfo Project => _project;

    public async Task ShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _dialog.XamlRoot = xamlRoot;
        await _dialog.ShowAsync();
    }

    private async void AddButton_Click(object sender, RoutedEventArgs args)
    {
        string name = _name.Text.Trim();
        string requirement = _requirement.Text.Trim();
        if (name.Length == 0 || requirement.Length == 0)
        {
            _status.Text = "Enter both a package name and a version requirement.";
            return;
        }
        await MutateAsync(
            (ProjectDependencyKind)Math.Max(0, _kind.SelectedIndex),
            name,
            requirement);
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs args)
    {
        if (_packageList.SelectedItem is not PackageRow selected)
        {
            _status.Text = "Select a package to remove.";
            return;
        }
        await MutateAsync(selected.Kind, selected.Name, null);
    }

    private async Task MutateAsync(
        ProjectDependencyKind kind,
        string name,
        string? requirement)
    {
        SetBusy(true);
        try
        {
            ProjectResponse response = await _client.UpdateDependencyAsync(
                _project.RootPath,
                kind,
                name,
                requirement);
            if (!response.Success || response.Project is null)
            {
                _status.Text = string.Join(" ", response.Diagnostics.Select(item => item.Message));
                return;
            }
            _project = response.Project;
            RefreshRows();
            _status.Text = requirement is null
                ? $"Removed {name} from sprout.toml."
                : $"Saved {name} {requirement} in sprout.toml.";
            _name.Text = string.Empty;
            _requirement.Text = string.Empty;
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _addButton.IsEnabled = !busy;
        _removeButton.IsEnabled = !busy;
        _status.Text = busy ? "Updating the project manifest…" : _status.Text;
    }

    private void RefreshRows()
    {
        _packages.Clear();
        foreach ((string name, string requirement) in _project.Dependencies
            ?? new Dictionary<string, string>())
        {
            _packages.Add(new PackageRow(ProjectDependencyKind.Sprout, name, requirement));
        }
        foreach ((string name, string requirement) in _project.PythonDependencies
            ?? new Dictionary<string, string>())
        {
            _packages.Add(new PackageRow(ProjectDependencyKind.Python, name, requirement));
        }
    }

    private static DataTemplate BuildItemTemplate()
    {
        const string template = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Padding="10,7" ColumnSpacing="12">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="100" />
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="180" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding KindLabel}" Foreground="{StaticResource StudioSuccessBrush}" />
                    <TextBlock Grid.Column="1" Text="{Binding Name}" FontFamily="Consolas" />
                    <TextBlock Grid.Column="2" Text="{Binding Requirement}" FontFamily="Consolas" />
                </Grid>
            </DataTemplate>
            """;
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(template);
    }
}

internal sealed record PackageRow(
    ProjectDependencyKind Kind,
    string Name,
    string Requirement)
{
    public string KindLabel => Kind == ProjectDependencyKind.Python ? "Python" : "Sprout";
}
