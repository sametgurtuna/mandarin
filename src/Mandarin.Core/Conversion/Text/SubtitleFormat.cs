using System.Text;
using System.Text.RegularExpressions;

namespace Mandarin.Core.Conversion.Text;

/// <summary>
/// A minimal, dependency-free reader/writer for SRT and WebVTT subtitle cues, and for
/// treating plain text as a sequence of evenly-timed cues.
/// </summary>
public static partial class SubtitleFormat
{
    private static readonly TimeSpan DefaultCueDuration = TimeSpan.FromSeconds(3);

    public static List<SubtitleCue> ParseSrt(string content)
    {
        var cues = new List<SubtitleCue>();
        var blocks = content.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var timeLineIndex = Array.FindIndex(lines, l => l.Contains("-->"));
            if (timeLineIndex < 0)
            {
                continue;
            }

            var match = SrtTimeRegex().Match(lines[timeLineIndex]);
            if (!match.Success)
            {
                continue;
            }

            var start = ParseSrtTimestamp(match.Groups[1].Value);
            var end = ParseSrtTimestamp(match.Groups[2].Value);
            var text = string.Join('\n', lines.Skip(timeLineIndex + 1));
            cues.Add(new SubtitleCue(start, end, text));
        }

        return cues;
    }

    public static List<SubtitleCue> ParseVtt(string content)
    {
        var cues = new List<SubtitleCue>();
        var normalized = content.Replace("\r\n", "\n");
        var blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var timeLineIndex = Array.FindIndex(lines, l => l.Contains("-->"));
            if (timeLineIndex < 0)
            {
                continue;
            }

            var match = VttTimeRegex().Match(lines[timeLineIndex]);
            if (!match.Success)
            {
                continue;
            }

            var start = ParseVttTimestamp(match.Groups[1].Value);
            var end = ParseVttTimestamp(match.Groups[2].Value);
            var text = string.Join('\n', lines.Skip(timeLineIndex + 1));
            cues.Add(new SubtitleCue(start, end, text));
        }

        return cues;
    }

    /// <summary>
    /// Treats each non-blank line of plain text as its own cue, with a fixed duration
    /// played back-to-back starting at 00:00:00.
    /// </summary>
    public static List<SubtitleCue> FromPlainText(string content)
    {
        var cues = new List<SubtitleCue>();
        var start = TimeSpan.Zero;

        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var end = start + DefaultCueDuration;
            cues.Add(new SubtitleCue(start, end, line.Trim()));
            start = end;
        }

        return cues;
    }

    public static string ToSrt(IReadOnlyList<SubtitleCue> cues)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < cues.Count; i++)
        {
            sb.Append(i + 1).Append('\n');
            sb.Append(FormatSrtTimestamp(cues[i].Start)).Append(" --> ").Append(FormatSrtTimestamp(cues[i].End)).Append('\n');
            sb.Append(cues[i].Text).Append('\n').Append('\n');
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    public static string ToVtt(IReadOnlyList<SubtitleCue> cues)
    {
        var sb = new StringBuilder("WEBVTT\n\n");
        foreach (var cue in cues)
        {
            sb.Append(FormatVttTimestamp(cue.Start)).Append(" --> ").Append(FormatVttTimestamp(cue.End)).Append('\n');
            sb.Append(cue.Text).Append('\n').Append('\n');
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    public static string ToPlainText(IReadOnlyList<SubtitleCue> cues) =>
        string.Join(Environment.NewLine + Environment.NewLine, cues.Select(c => c.Text));

    private static TimeSpan ParseSrtTimestamp(string value)
    {
        // HH:MM:SS,mmm
        var parts = value.Split(',');
        var hms = parts[0].Split(':');
        return new TimeSpan(0, int.Parse(hms[0]), int.Parse(hms[1]), int.Parse(hms[2]), int.Parse(parts[1]));
    }

    private static TimeSpan ParseVttTimestamp(string value)
    {
        // HH:MM:SS.mmm (hours optional in strict VTT, but we require it upstream via regex)
        var parts = value.Split('.');
        var hms = parts[0].Split(':');
        return new TimeSpan(0, int.Parse(hms[0]), int.Parse(hms[1]), int.Parse(hms[2]), int.Parse(parts[1]));
    }

    private static string FormatSrtTimestamp(TimeSpan t) =>
        $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2},{t.Milliseconds:D3}";

    private static string FormatVttTimestamp(TimeSpan t) =>
        $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}.{t.Milliseconds:D3}";

    [GeneratedRegex(@"(\d{2}:\d{2}:\d{2},\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2},\d{3})")]
    private static partial Regex SrtTimeRegex();

    [GeneratedRegex(@"(\d{2}:\d{2}:\d{2}\.\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}\.\d{3})")]
    private static partial Regex VttTimeRegex();
}
