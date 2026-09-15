using System.Windows;
using System.Windows.Controls;
using Mandarin.App.Resources;
using Mandarin.Core.AdvancedTools;

namespace Mandarin.App.Views;

/// <summary>
/// The "→ photo (cropped).jpg, saved next to the original" line the tool dialogs
/// show before the user commits. The names come from
/// <see cref="ToolOutputNaming"/>, the same helper the tools themselves use, so
/// the preview always matches the file that gets written — including the "(2)"
/// suffix when something with that name already exists.
/// </summary>
internal static class OutputPreview
{
    public static void Show(TextBlock target, string? sourcePath, string suffix, string? extensionOverride = null)
    {
        if (string.IsNullOrEmpty(sourcePath))
        {
            target.Visibility = Visibility.Collapsed;
            return;
        }

        target.Text = Strings.OutputPreview(ToolOutputNaming.PreviewFileName(sourcePath, suffix, extensionOverride));
        target.Visibility = Visibility.Visible;
    }

    public static void Show(TextBlock target, string? sourcePath, AdvancedToolKind kind, string? extensionOverride = null) =>
        Show(target, sourcePath, ToolOutputNaming.SuffixFor(kind), extensionOverride);

    /// <summary>For tools that write two files, such as Split.</summary>
    public static void ShowPair(TextBlock target, string? sourcePath, string firstSuffix, string secondSuffix)
    {
        if (string.IsNullOrEmpty(sourcePath))
        {
            target.Visibility = Visibility.Collapsed;
            return;
        }

        target.Text = Strings.OutputPreviewPair(
            ToolOutputNaming.PreviewFileName(sourcePath, firstSuffix),
            ToolOutputNaming.PreviewFileName(sourcePath, secondSuffix));
        target.Visibility = Visibility.Visible;
    }
}
