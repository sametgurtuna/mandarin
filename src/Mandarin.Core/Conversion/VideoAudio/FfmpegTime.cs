using System.Globalization;

namespace Mandarin.Core.Conversion.VideoAudio;

public static class FfmpegTime
{
    public static string Format(TimeSpan time) =>
        time.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
