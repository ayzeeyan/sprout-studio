using Avalonia.Controls;
using SproutStudio.ViewModels;
using SproutStudio.Views.Documentation;

namespace SproutStudio.Views.Editor;

public sealed partial class AcademyDocumentView : UserControl
{
    private readonly AvaloniaMarkdownRenderer _renderer = new(_ => Task.CompletedTask);

    public AcademyDocumentView()
    {
        InitializeComponent();
        DataContextChanged += AcademyDocumentView_DataContextChanged;
        AttachedToVisualTree += (_, _) => RenderLesson();
    }

    private void AcademyDocumentView_DataContextChanged(object? sender, EventArgs args) => RenderLesson();

    private void RenderLesson()
    {
        ContentControl? host = this.FindControl<ContentControl>("LessonContentHost");
        if (host is null)
        {
            return;
        }
        host.Content = DataContext is AcademyDocumentTabViewModel viewModel
            ? _renderer.Render(viewModel.Lesson.Content).Content
            : null;
    }
}
