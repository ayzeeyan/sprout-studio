namespace SproutStudio.Academy;

/// <summary>One calendar day of Academy practice.</summary>
public sealed record LearningActivityDay(DateOnly Date, int Attempts, int XpEarned)
{
    public bool Practiced => Attempts > 0;
}

/// <summary>
/// Habit metrics derived from submission history: streaks, today's goal progress, and a
/// fixed-length activity calendar (oldest day first) for heatmaps.
/// </summary>
public sealed record LearningHabitSummary(
    int CurrentStreak,
    int LongestStreak,
    bool PracticedToday,
    int DailyXpGoal,
    int XpEarnedToday,
    LearningActivityDay[] Calendar)
{
    public static LearningHabitSummary Empty { get; } = new(0, 0, false, LearningHabits.DefaultDailyXpGoal, 0, []);

    public int DailyGoalPercent => DailyXpGoal <= 0
        ? 100
        : Math.Clamp(XpEarnedToday * 100 / DailyXpGoal, 0, 100);

    public bool DailyGoalMet => XpEarnedToday >= DailyXpGoal;
}

/// <summary>An XP-earning or practice event on a specific instant.</summary>
public sealed record LearningEvent(DateTimeOffset At, int Xp);

public static class LearningHabits
{
    public const int DefaultDailyXpGoal = 30;

    public const int DefaultCalendarDays = 84;

    /// <summary>
    /// Summarizes practice events. Every submission counts as practice for its day; XP comes
    /// from first passes. A streak stays alive through today until midnight, so a learner who
    /// practiced yesterday but not yet today still sees their streak.
    /// </summary>
    public static LearningHabitSummary Summarize(
        IEnumerable<LearningEvent> events,
        DateOnly today,
        TimeZoneInfo zone,
        int dailyXpGoal = DefaultDailyXpGoal,
        int calendarDays = DefaultCalendarDays)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(zone);
        Dictionary<DateOnly, (int Attempts, int Xp)> byDay = [];
        foreach (LearningEvent item in events)
        {
            DateOnly day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.At, zone).DateTime);
            (int attempts, int xp) = byDay.GetValueOrDefault(day);
            byDay[day] = (attempts + 1, xp + Math.Max(0, item.Xp));
        }

        bool practicedToday = byDay.ContainsKey(today);
        int current = 0;
        for (DateOnly day = practicedToday ? today : today.AddDays(-1); byDay.ContainsKey(day); day = day.AddDays(-1))
        {
            current++;
        }

        int longest = 0;
        int run = 0;
        DateOnly? previous = null;
        foreach (DateOnly day in byDay.Keys.Order())
        {
            run = previous is { } last && last.AddDays(1) == day ? run + 1 : 1;
            longest = Math.Max(longest, run);
            previous = day;
        }

        int days = Math.Max(0, calendarDays);
        LearningActivityDay[] calendar = Enumerable.Range(0, days)
            .Select(offset => today.AddDays(offset - days + 1))
            .Select(day =>
            {
                (int attempts, int xp) = byDay.GetValueOrDefault(day);
                return new LearningActivityDay(day, attempts, xp);
            })
            .ToArray();

        return new LearningHabitSummary(
            current,
            longest,
            practicedToday,
            Math.Max(0, dailyXpGoal),
            byDay.GetValueOrDefault(today).Xp,
            calendar);
    }
}
