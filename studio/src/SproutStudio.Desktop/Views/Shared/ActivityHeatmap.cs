using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SproutStudio.Academy;

namespace SproutStudio.Views.Shared;

/// <summary>
/// A week-column heatmap of Academy practice (like a contribution graph): one rounded cell per
/// day, rows Monday–Sunday, shaded by attempts using the current accent color.
/// </summary>
public sealed class ActivityHeatmap : Control
{
    public static readonly StyledProperty<IReadOnlyList<LearningActivityDay>?> DaysProperty =
        AvaloniaProperty.Register<ActivityHeatmap, IReadOnlyList<LearningActivityDay>?>(nameof(Days));

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<ActivityHeatmap, double>(nameof(CellSize), 11);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ActivityHeatmap, double>(nameof(Gap), 3);

    static ActivityHeatmap()
    {
        AffectsMeasure<ActivityHeatmap>(DaysProperty, CellSizeProperty, GapProperty);
        AffectsRender<ActivityHeatmap>(DaysProperty, CellSizeProperty, GapProperty);
    }

    public IReadOnlyList<LearningActivityDay>? Days
    {
        get => GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int weeks = WeekCount();
        double pitch = CellSize + Gap;
        return new Size(Math.Max(0, (weeks * pitch) - Gap), Math.Max(0, (7 * pitch) - Gap));
    }

    public override void Render(DrawingContext context)
    {
        IReadOnlyList<LearningActivityDay>? days = Days;
        if (days is null || days.Count == 0)
        {
            return;
        }

        Color accent = this.TryFindResource("StudioAccentColor", ActualThemeVariant, out object? value) && value is Color color
            ? color
            : Color.Parse("#4CD484");
        IBrush empty = new SolidColorBrush(Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF));
        IBrush[] levels =
        [
            new SolidColorBrush(Color.FromArgb(0x55, accent.R, accent.G, accent.B)),
            new SolidColorBrush(Color.FromArgb(0x90, accent.R, accent.G, accent.B)),
            new SolidColorBrush(Color.FromArgb(0xC8, accent.R, accent.G, accent.B)),
            new SolidColorBrush(accent),
        ];
        IPen todayPen = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)), 1);

        double pitch = CellSize + Gap;
        DateOnly first = days[0].Date;
        int firstRow = DayRow(first);
        DateOnly last = days[^1].Date;
        for (int index = 0; index < days.Count; index++)
        {
            LearningActivityDay day = days[index];
            int slot = firstRow + index;
            var cell = new Rect(slot / 7 * pitch, slot % 7 * pitch, CellSize, CellSize);
            IBrush fill = day.Attempts switch
            {
                0 => empty,
                1 => levels[0],
                2 => levels[1],
                <= 4 => levels[2],
                _ => levels[3],
            };
            context.DrawRectangle(fill, day.Date == last ? todayPen : null, cell, 2.5, 2.5);
        }
    }

    private int WeekCount()
    {
        IReadOnlyList<LearningActivityDay>? days = Days;
        return days is null || days.Count == 0 ? 0 : ((DayRow(days[0].Date) + days.Count - 1) / 7) + 1;
    }

    /// <summary>Monday = row 0 … Sunday = row 6.</summary>
    private static int DayRow(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;
}
