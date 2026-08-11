using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SproutStudio.Academy;

namespace SproutStudio;

internal sealed class AcademyProfileForm
{
    private readonly TextBox _displayName = new()
    {
        Header = "Display name",
        PlaceholderText = "How should Sprout Studio address you?",
        MaxLength = 60,
    };
    private readonly TextBox _username = new()
    {
        Header = "Preferred username",
        PlaceholderText = "sprout_learner",
        MaxLength = 32,
    };
    private readonly ComboBox _experience = new()
    {
        Header = "Experience level",
        HorizontalAlignment = HorizontalAlignment.Stretch,
        DisplayMemberPath = nameof(ExperienceChoice.Label),
    };
    private readonly CheckBox _showGamification = new()
    {
        Content = "Show XP, levels, and achievements",
        IsChecked = true,
    };
    private readonly Dictionary<ProgrammingGoal, CheckBox> _goals = [];
    private bool _loading;

    public AcademyProfileForm()
    {
        Root = new StackPanel { Spacing = 12 };
        Root.Children.Add(new TextBlock
        {
            Text = "This profile stays on this computer. Sprout Studio does not ask for email, age, or other sensitive information.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        });
        Root.Children.Add(_displayName);
        Root.Children.Add(_username);

        _experience.ItemsSource = new[]
        {
            new ExperienceChoice(ExperienceLevel.AbsoluteBeginner, "Absolute Beginner"),
            new ExperienceChoice(ExperienceLevel.Intermediate, "Intermediate"),
            new ExperienceChoice(ExperienceLevel.Advanced, "Advanced"),
        };
        _experience.SelectedIndex = 0;
        _experience.SelectionChanged += Experience_SelectionChanged;
        Root.Children.Add(_experience);

        Root.Children.Add(new TextBlock
        {
            Text = "Programming goals",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        var goals = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        goals.ColumnDefinitions.Add(new ColumnDefinition());
        goals.ColumnDefinitions.Add(new ColumnDefinition());
        ProgrammingGoal[] values = Enum.GetValues<ProgrammingGoal>();
        for (int index = 0; index < values.Length; index++)
        {
            if (index % 2 == 0)
            {
                goals.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            ProgrammingGoal goal = values[index];
            var checkBox = new CheckBox
            {
                Content = RecommendationEngine.DisplayGoal(goal),
                IsChecked = goal == ProgrammingGoal.GeneralProgramming,
            };
            _goals.Add(goal, checkBox);
            Grid.SetRow(checkBox, index / 2);
            Grid.SetColumn(checkBox, index % 2);
            goals.Children.Add(checkBox);
        }
        Root.Children.Add(goals);
        Root.Children.Add(_showGamification);
    }

    public StackPanel Root { get; }

    public void Load(AcademyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _loading = true;
        try
        {
            _displayName.Text = profile.DisplayName;
            _username.Text = profile.PreferredUsername;
            _experience.SelectedIndex = (int)profile.Experience;
            foreach ((ProgrammingGoal goal, CheckBox checkBox) in _goals)
            {
                checkBox.IsChecked = profile.Goals.Contains(goal);
            }
            _showGamification.IsChecked = profile.ShowGamification;
        }
        finally
        {
            _loading = false;
        }
    }

    public AcademyProfile ReadProfile()
    {
        ExperienceLevel experience = (_experience.SelectedItem as ExperienceChoice)?.Value
            ?? ExperienceLevel.AbsoluteBeginner;
        return AcademyProfileValidator.ValidateAndNormalize(new AcademyProfile(
            _displayName.Text,
            _username.Text,
            experience,
            _goals.Where(item => item.Value.IsChecked == true)
                .Select(item => item.Key)
                .ToArray(),
            _showGamification.IsChecked == true));
    }

    private void Experience_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_loading || _experience.SelectedItem is not ExperienceChoice choice)
        {
            return;
        }
        _showGamification.IsChecked = choice.Value != ExperienceLevel.Advanced;
    }

    private sealed record ExperienceChoice(ExperienceLevel Value, string Label);
}
