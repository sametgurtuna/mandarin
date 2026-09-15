using System.Windows;
using System.Windows.Controls;
using Mandarin.Core.AdvancedTools;

namespace Mandarin.App.Views;

public partial class PdfSplitWindow : Window
{
    private readonly int? _pageCount;
    private readonly string? _sourcePath;

    public PdfSplitOptions? Result { get; private set; }

    public PdfSplitWindow(int? pageCount, string? sourcePath = null)
    {
        InitializeComponent();
        DialogChrome.Apply(this, HeaderBar, CardRoot);
        _pageCount = pageCount;

        PageCountText.Text = pageCount is { } count
            ? $"This PDF has {count} page(s). Pick a range to extract into a new PDF."
            : "Couldn't determine the page count. Pick a range to extract into a new PDF.";

        ToPageTextBox.Text = pageCount?.ToString() ?? string.Empty;
        _sourcePath = sourcePath;
        UpdateOutputPreview();
    }

    private void UpdateOutputPreview()
    {
        // TextChanged fires while the XAML is still being parsed (the From box has
        // a literal Text="1"), so the preview control may not exist yet.
        if (OutputPreviewText is null)
        {
            return;
        }

        if (!int.TryParse(FromPageTextBox.Text, out var from) ||
            !int.TryParse(ToPageTextBox.Text, out var to) ||
            from < 1 || to < from)
        {
            OutputPreviewText.Visibility = Visibility.Collapsed;
            return;
        }

        OutputPreview.Show(OutputPreviewText, _sourcePath, ToolOutputNaming.PageRangeSuffix(from, to));
    }

    // Typing is the user's correction to whatever the error complained about, so
    // the message should get out of the way as soon as they start.
    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ErrorText is not null)
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }

        UpdateOutputPreview();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(FromPageTextBox.Text, out var fromPage) ||
            !int.TryParse(ToPageTextBox.Text, out var toPage))
        {
            ShowError("Enter whole page numbers.");
            return;
        }

        if (fromPage < 1 || toPage < fromPage)
        {
            ShowError("'From page' must be at least 1 and no greater than 'To page'.");
            return;
        }

        if (_pageCount is { } count && toPage > count)
        {
            ShowError($"This PDF only has {count} page(s).");
            return;
        }

        Result = new PdfSplitOptions(fromPage, toPage);
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
