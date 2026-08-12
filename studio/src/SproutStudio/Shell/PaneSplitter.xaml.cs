using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace SproutStudio.Shell;

public sealed partial class PaneSplitter : UserControl
{
    private Point _lastPosition;
    private bool _dragging;
    private Orientation _orientation = Orientation.Vertical;

    public PaneSplitter()
    {
        InitializeComponent();
        ApplyOrientation();
    }

    public event EventHandler<PaneDragEventArgs>? DragDelta;

    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            ApplyOrientation();
        }
    }

    private void ApplyOrientation()
    {
        if (Divider is null)
        {
            return;
        }

        bool vertical = Orientation == Orientation.Vertical;
        Divider.Width = vertical ? 1 : double.NaN;
        Divider.Height = vertical ? double.NaN : 1;
        Divider.HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        Divider.VerticalAlignment = vertical ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        ProtectedCursor = InputSystemCursor.Create(
            vertical ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);
    }

    private void SplitterSurface_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        Divider.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StudioAccentBrush"];

    private void SplitterSurface_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            Divider.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StudioBorderBrush"];
        }
    }

    private void SplitterSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        _lastPosition = e.GetCurrentPoint(null).Position;
        SplitterSurface.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void SplitterSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        Point position = e.GetCurrentPoint(null).Position;
        double delta = Orientation == Orientation.Vertical
            ? position.X - _lastPosition.X
            : position.Y - _lastPosition.Y;
        _lastPosition = position;
        if (Math.Abs(delta) > double.Epsilon)
        {
            DragDelta?.Invoke(this, new PaneDragEventArgs(delta));
        }
        e.Handled = true;
    }

    private void SplitterSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        SplitterSurface.ReleasePointerCapture(e.Pointer);
        Divider.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["StudioBorderBrush"];
        e.Handled = true;
    }
}

public sealed class PaneDragEventArgs(double delta) : EventArgs
{
    public double Delta { get; } = delta;
}
