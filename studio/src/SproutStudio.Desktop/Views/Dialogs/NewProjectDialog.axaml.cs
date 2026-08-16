using Avalonia.Controls;
using Avalonia.Interactivity;
using SproutStudio.Core;
using SproutStudio.Models;

namespace SproutStudio.Views.Dialogs;

public sealed partial class NewProjectDialog : Window
{
    private readonly string _parentPath;

    public NewProjectDialog()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
    {
    }

    public NewProjectDialog(string parentPath)
    {
        _parentPath = Path.GetFullPath(parentPath);
        InitializeComponent();
        this.FindControl<TextBlock>("ParentPathText")!.Text = _parentPath;
        UpdateValidation();
    }

    private void NameBox_TextChanged(object? sender, TextChangedEventArgs args) => UpdateValidation();

    private void UpdateValidation()
    {
        TextBox nameBox = this.FindControl<TextBox>("NameBox")!;
        string name = nameBox.Text?.Trim() ?? string.Empty;
        string? error = ValidateName(name);
        string destination = string.IsNullOrEmpty(name) ? _parentPath : Path.Combine(_parentPath, name);
        this.FindControl<TextBlock>("DestinationText")!.Text = destination;
        this.FindControl<TextBlock>("ValidationText")!.Text = name.Length == 0
            ? string.Empty
            : error ?? string.Empty;
        this.FindControl<Button>("CreateButton")!.IsEnabled = error is null;
    }

    private string? ValidateName(string name)
    {
        if (name.Length is < 1 or > 60)
        {
            return "Use 1–60 characters.";
        }
        if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "The project name contains invalid characters.";
        }
        if (Directory.Exists(Path.Combine(_parentPath, name))
            || File.Exists(Path.Combine(_parentPath, name)))
        {
            return "That destination already exists.";
        }
        return null;
    }

    private void Create_Click(object? sender, RoutedEventArgs args)
    {
        string name = this.FindControl<TextBox>("NameBox")!.Text!.Trim();
        if (ValidateName(name) is not null)
        {
            UpdateValidation();
            return;
        }
        ProjectTemplate template = this.FindControl<ComboBox>("TemplateBox")!.SelectedIndex switch
        {
            1 => ProjectTemplate.Structured,
            2 => ProjectTemplate.Advanced,
            _ => ProjectTemplate.Guided,
        };
        Close(new ProjectCreationRequest(Path.Combine(_parentPath, name), name, template));
    }

    private void Cancel_Click(object? sender, RoutedEventArgs args) =>
        Close((ProjectCreationRequest?)null);
}
