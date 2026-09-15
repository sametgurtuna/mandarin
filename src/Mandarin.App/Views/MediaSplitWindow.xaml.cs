using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Mandarin.Core.AdvancedTools;

namespace Mandarin.App.Views;

public partial class MediaSplitWindow : Window
{
    private readonly TimeSpan? _duration;

    public MediaSplitOptions? Result { get; private set; }

    public MediaSplitWindow(TimeSpan? duration, string? sourcePath = null)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        _duration = duration;

        DurationText.Text = duration is { } d
            ? $"Length: {FormatTime(d)}. Produces two files: before and after this point."
            : "Couldn't determine the file's length. Produces two files: before and after this point.";

        OutputPreview.ShowPair(OutputPreviewText, sourcePath, ToolOutputNaming.SplitSuffix(1), ToolOutputNaming.SplitSuffix(2));
    }

    // Typing is the user's correction to whatever the error complained about, so
    // the message should get out of the way as soon as they start.
    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ErrorText is not null)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

    private static bool TryParseTime(string text, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        var parts = text.Trim().Split(':');

        try
        {
            if (parts.Length == 2)
            {
                result = TimeSpan.FromMinutes(int.Parse(parts[0], CultureInfo.InvariantCulture)) +
                         TimeSpan.FromSeconds(double.Parse(parts[1], CultureInfo.InvariantCulture));
                return true;
            }

            if (parts.Length == 1)
            {
                result = TimeSpan.FromSeconds(double.Parse(parts[0], CultureInfo.InvariantCulture));
                return true;
            }
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }

        return false;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseTime(AtTextBox.Text, out var at) || at <= TimeSpan.Zero)
        {
            ShowError("Enter a time as mm:ss (e.g. 1:30).");
            return;
        }

        if (_duration is { } duration && at >= duration)
        {
            ShowError($"The file is only {FormatTime(duration)} long.");
            return;
        }

        Result = new MediaSplitOptions(at);
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
