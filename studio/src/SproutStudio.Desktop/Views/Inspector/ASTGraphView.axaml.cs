using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SproutStudio.ViewModels;

namespace SproutStudio.Views.Inspector;

public sealed partial class ASTGraphView : UserControl
{
    private ASTGraphViewModel? _viewModel;

    public ASTGraphView()
    {
        InitializeComponent();
        DataContextChanged += ASTGraphView_DataContextChanged;
    }

    private void ASTGraphView_DataContextChanged(object? sender, EventArgs args)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.RootNodes.CollectionChanged -= RootNodes_CollectionChanged;
        }

        _viewModel = DataContext as ASTGraphViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.RootNodes.CollectionChanged += RootNodes_CollectionChanged;
        }

        RenderCanvasGraph();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ASTGraphViewModel.IsVisualGraphView)
            or nameof(ASTGraphViewModel.SelectedCategory)
            or nameof(ASTGraphViewModel.SearchText)
            or nameof(ASTGraphViewModel.TotalNodeCount))
        {
            RenderCanvasGraph();
        }
    }

    private void RootNodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        RenderCanvasGraph();

    private void RenderCanvasGraph()
    {
        if (AstCanvas is null)
        {
            return;
        }

        AstCanvas.Children.Clear();

        if (_viewModel is null || _viewModel.RootNodes.Count == 0)
        {
            AstCanvas.Width = 0;
            AstCanvas.Height = 0;
            return;
        }

        double startX = 20;
        double currentY = 20;
        double maxX = 600;

        foreach (AstNodeViewModel root in _viewModel.RootNodes)
        {
            if (root.IsVisible)
            {
                (double width, double height) = LayoutNode(root, startX, ref currentY, ref maxX);
                currentY += 24;
            }
        }

        AstCanvas.Width = Math.Max(600, maxX + 40);
        AstCanvas.Height = Math.Max(400, currentY + 40);
    }

    private (double Width, double Height) LayoutNode(
        AstNodeViewModel node,
        double x,
        ref double currentY,
        ref double maxX)
    {
        double nodeY = currentY;
        double nodeWidth = 220;
        double nodeHeight = 56;

        if (x + nodeWidth > maxX)
        {
            maxX = x + nodeWidth;
        }

        // Create visual card for node
        Border card = CreateNodeCard(node, nodeWidth, nodeHeight);
        Canvas.SetLeft(card, x);
        Canvas.SetTop(card, nodeY);
        AstCanvas.Children.Add(card);

        currentY += nodeHeight + 16;

        double childX = x + nodeWidth + 36;
        var visibleChildren = node.Children.Where(c => c.IsVisible).ToList();

        if (visibleChildren.Count > 0)
        {
            double firstChildY = currentY;
            double lastChildY = currentY;

            for (int i = 0; i < visibleChildren.Count; i++)
            {
                AstNodeViewModel child = visibleChildren[i];
                double childStartY = currentY;
                LayoutNode(child, childX, ref currentY, ref maxX);

                if (i == 0) firstChildY = childStartY + (nodeHeight / 2);
                lastChildY = childStartY + (nodeHeight / 2);

                // Draw connector line from parent to child
                DrawConnector(x + nodeWidth, nodeY + (nodeHeight / 2), childX, childStartY + (nodeHeight / 2), node.BadgeColor);
            }
        }

        return (nodeWidth, nodeHeight);
    }

    private Border CreateNodeCard(AstNodeViewModel node, double width, double height)
    {
        Color badgeColor = Color.Parse(node.BadgeColor);
        Color badgeBg = Color.Parse(node.BadgeBackground);

        var border = new Border
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(220, 24, 28, 25)),
            BorderBrush = new SolidColorBrush(badgeColor),
            BorderThickness = new Thickness(node.IsSelected ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6),
            Cursor = new Cursor(StandardCursorType.Hand),
            ClipToBounds = true,
        };

        var contentGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };

        // Kind badge
        var badge = new Border
        {
            Background = new SolidColorBrush(badgeBg),
            BorderBrush = new SolidColorBrush(badgeColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1),
            Margin = new Thickness(0, 0, 6, 4),
            Child = new TextBlock
            {
                Text = $"{node.Icon} {node.Kind}",
                Foreground = new SolidColorBrush(badgeColor),
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
            },
        };
        Grid.SetRow(badge, 0);
        Grid.SetColumn(badge, 0);
        contentGrid.Children.Add(badge);

        // Location text
        var locationText = new TextBlock
        {
            Text = $"L{node.Span.StartLine}:{node.Span.StartColumn}",
            Foreground = new SolidColorBrush(Color.Parse("#7E8980")),
            FontSize = 9,
            FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(locationText, 0);
        Grid.SetColumn(locationText, 1);
        contentGrid.Children.Add(locationText);

        // Label
        var labelText = new TextBlock
        {
            Text = string.IsNullOrEmpty(node.Detail) ? node.Label : $"{node.Label} ({node.Detail})",
            Foreground = new SolidColorBrush(Color.Parse("#EEF2EE")),
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(labelText, 1);
        Grid.SetColumnSpan(labelText, 2);
        contentGrid.Children.Add(labelText);

        border.Child = contentGrid;

        border.PointerPressed += (_, _) =>
        {
            if (_viewModel is not null)
            {
                _viewModel.SelectedNode = node;
            }
        };

        return border;
    }

    private void DrawConnector(double x1, double y1, double x2, double y2, string colorHex)
    {
        Color color = Color.Parse(colorHex);
        double midX = x1 + ((x2 - x1) / 2);

        var path = new Avalonia.Controls.Shapes.Path
        {
            Stroke = new SolidColorBrush(Color.FromArgb(140, color.R, color.G, color.B)),
            StrokeThickness = 1.5,
            Data = new PathGeometry
            {
                Figures =
                [
                    new PathFigure
                    {
                        StartPoint = new Point(x1, y1),
                        IsFilled = false,
                        Segments =
                        [
                            new BezierSegment
                            {
                                Point1 = new Point(midX, y1),
                                Point2 = new Point(midX, y2),
                                Point3 = new Point(x2, y2),
                            }
                        ]
                    }
                ]
            }
        };

        AstCanvas.Children.Add(path);
    }
}
