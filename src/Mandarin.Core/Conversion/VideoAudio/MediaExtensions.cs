namespace Mandarin.Core.Conversion.VideoAudio;

/// <summary>
/// Shared video/audio extension sets, used by both the format converter and the
/// ffmpeg-backed advanced tools (compress, crop, trim, split, strip metadata).
/// </summary>
public static class MediaExtensions
{
    public static readonly IReadOnlySet<string> VideoExtensions = new HashSet<string>
    {
        "mp4", "mov", "mkv", "webm", "avi", "wmv", "gif",
    };

    public static readonly IReadOnlySet<string> AudioExtensions = new HashSet<string>
    {
        "mp3", "m4a", "wav", "flac", "ogg", "opus", "aiff", "wma",
    };

    public static readonly IReadOnlySet<string> AllExtensions =
        VideoExtensions.Union(AudioExtensions).ToHashSet();
}
