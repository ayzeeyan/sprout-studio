using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SproutStudio.Core;
using SproutStudio.Editor;
using SproutStudio.Models;

namespace SproutStudio.ViewModels;

public sealed class WorkspaceNodeViewModel : ObservableObject
{
    private bool _isExpanded = true;

    public WorkspaceNodeViewModel(
        string name,
        string relativePath,
        string? fullPath,
        bool isDirectory)
    {
        Name = name;
        RelativePath = relativePath;
        FullPath = fullPath;
        IsDirectory = isDirectory;
    }

    public string Name { get; }

    public string RelativePath { get; }

    public string? FullPath { get; }

    public bool IsDirectory { get; }

    public bool IsFile => !IsDirectory;

    public string IconGlyph => "\uE8B7";

    public string LanguageKey => IsDirectory
        ? "text"
        : LanguageRegistry.ForPath(Name).Key;

    public ObservableCollection<WorkspaceNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

}

public sealed record ProblemItemViewModel(
    StudioDiagnostic Diagnostic,
    string Severity,
    string Code,
    string Title,
    string Message,
    string FileName,
    int Line,
    int Column)
{
    public static ProblemItemViewModel FromDiagnostic(StudioDiagnostic diagnostic) => new(
        diagnostic,
        diagnostic.Severity,
        diagnostic.Code,
        diagnostic.Title,
        diagnostic.Message,
        Path.GetFileName(diagnostic.Span.File),
        diagnostic.Span.StartLine,
        diagnostic.Span.StartColumn);
}

public sealed record ConsoleLineViewModel(string Text, ConsoleLineKind Kind)
{
    public bool IsMuted => Kind == ConsoleLineKind.Muted;
    public bool IsSuccess => Kind == ConsoleLineKind.Success;
    public bool IsWarning => Kind == ConsoleLineKind.Warning;
    public bool IsError => Kind == ConsoleLineKind.Error;
}

public sealed record OutlineItemViewModel(
    string Name,
    string Kind,
    string Detail,
    SourceSpan Span,
    int Depth)
{
    public string DisplayName => $"{new string(' ', Depth * 2)}{Name}";
}

public sealed record DebugVariableViewModel(string Name, string TypeName, string Value);

public sealed record DebugFrameViewModel(string Name, string Location, SourceSpan Span);

public sealed record BreakpointViewModel(int Line, string FileName, bool IsEnabled = true);

public sealed record RecentProjectViewModel(
    string Name,
    string RootPath,
    DateTimeOffset LastOpenedUtc,
    bool IsAvailable)
{
    public string Status => IsAvailable
        ? LastOpenedUtc.ToLocalTime().ToString("MMM d, yyyy")
        : "Project not found";

    public string ToolTip => IsAvailable
        ? RootPath
        : $"This project was moved or deleted: {RootPath}";

    public double RowOpacity => IsAvailable ? 1 : 0.65;

    public static RecentProjectViewModel FromModel(RecentProject project)
    {
        string manifestPath = Path.Combine(project.RootPath, "sprout.toml");
        bool isAvailable = Directory.Exists(project.RootPath)
            && File.Exists(manifestPath);
        return new RecentProjectViewModel(
            project.Name,
            project.RootPath,
            project.LastOpenedUtc,
            isAvailable);
    }
}
