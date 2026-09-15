using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools;

/// <summary>
/// The one place that decides what an advanced tool's output file is called.
///
/// Tools use it to build their real output path, and the tool dialogs use it to
/// show the user that name before they commit — so the preview can't drift away
/// from what actually gets written.
/// </summary>
public static class ToolOutputNaming
{
    /// <summary>The " (compressed)"-style marker appended to the source's name.</summary>
    public static string SuffixFor(AdvancedToolKind kind) => kind switch
    {
        AdvancedToolKind.Compress => "(compressed)",
        AdvancedToolKind.Crop => "(cropped)",
        AdvancedToolKind.Trim => "(trimmed)",
        AdvancedToolKind.StripMetadata => "(no metadata)",
        AdvancedToolKind.EditPhoto => "(edited)",
        AdvancedToolKind.EditMetadata => "(metadata)",
        AdvancedToolKind.RedactPhoto or AdvancedToolKind.RedactVideo => "(redacted)",

        // Split writes two files and names them itself; callers that care use
        // SplitSuffix instead.
        AdvancedToolKind.Split => "(part 1)",
        _ => "(edited)",
    };

    /// <summary>Suffix for one part of a split, 1-based.</summary>
    public static string SplitSuffix(int partNumber) => $"(part {partNumber})";

    /// <summary>Suffix for a page range pulled out of a PDF.</summary>
    public static string PageRangeSuffix(int fromPage, int toPage) => $"(pages {fromPage}-{toPage})";

    /// <summary>
    /// The file name the tool would write right now, without creating anything.
    /// Includes the " (2)" de-duplication, so what the user is shown is the name
    /// they will actually get.
    /// </summary>
    public static string PreviewFileName(string sourcePath, string suffix, string? extensionOverride = null) =>
        Path.GetFileName(FileExtensions.BuildSuffixedOutputPath(sourcePath, suffix, extensionOverride));

    /// <summary>Convenience overload for tools with a fixed suffix.</summary>
    public static string PreviewFileName(string sourcePath, AdvancedToolKind kind, string? extensionOverride = null) =>
        PreviewFileName(sourcePath, SuffixFor(kind), extensionOverride);
}
