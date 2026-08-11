using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SproutStudio.Academy;

namespace SproutStudio;

internal sealed class AcademyProfileDialog
{
    private readonly AcademyService _academy;
    private readonly AcademyProfileForm _form = new();
    private readonly ContentDialog _dialog;
    private readonly TextBlock _status = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed),
    };

    public AcademyProfileDialog(AcademyService academy)
    {
        _academy = academy ?? throw new ArgumentNullException(nameof(academy));
        var content = new StackPanel { Spacing = 12, MinWidth = 520 };
        content.Children.Add(new TextBlock
        {
            Text = "Set up your local Academy profile",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        content.Children.Add(_form.Root);
        content.Children.Add(_status);
        _dialog = new ContentDialog
        {
            Title = "Welcome to Sprout Academy",
            Content = new ScrollViewer
            {
                Content = content,
                MaxHeight = 640,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            PrimaryButtonText = "Save profile",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Primary,
        };
        _dialog.Opened += (_, _) => Opened?.Invoke(this, EventArgs.Empty);
        _dialog.PrimaryButtonClick += Dialog_PrimaryButtonClick;
    }

    public event EventHandler? Opened;

    public AcademyProfile? Profile { get; private set; }

    public async Task<AcademyProfile?> ShowAsync(XamlRoot xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _dialog.XamlRoot = xamlRoot;
        ContentDialogResult result = await _dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? Profile : null;
    }

    private async void Dialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        args.Cancel = true;
        _dialog.IsPrimaryButtonEnabled = false;
        _status.Text = "Saving local profile…";
        try
        {
            AcademyProfile profile = _form.ReadProfile();
            await _academy.SaveProfileAsync(profile);
            Profile = profile;
            args.Cancel = false;
        }
        catch (Exception exception)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            _dialog.IsPrimaryButtonEnabled = true;
            deferral.Complete();
        }
    }
}
