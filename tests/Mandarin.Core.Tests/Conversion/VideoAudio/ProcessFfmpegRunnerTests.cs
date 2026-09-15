using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.Tests.Conversion.VideoAudio;

public class ProcessFfmpegRunnerTests : IDisposable
{
    private readonly string? _originalEnvPath =
        Environment.GetEnvironmentVariable("MANDARIN_FFMPEG_PATH");

    public void Dispose() =>
        Environment.SetEnvironmentVariable("MANDARIN_FFMPEG_PATH", _originalEnvPath);

    [Fact]
    public async Task RunAsync_WhenFfmpegNotFound_ReturnsFailureInsteadOfThrowing()
    {
        Environment.SetEnvironmentVariable("MANDARIN_FFMPEG_PATH", @"C:\definitely\not\a\real\path\ffmpeg.exe");
        var runner = new ProcessFfmpegRunner();

        var result = await runner.RunAsync(new[] { "-y", "-i", "in.mp4", "out.mp3" }, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ffmpeg.exe was not found", result.ErrorMessage);
    }

    [Fact]
    public void FindFfmpegPath_WithEnvVarPointingAtMissingFile_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("MANDARIN_FFMPEG_PATH", @"C:\nope\ffmpeg.exe");

        var path = FfmpegLocator.FindFfmpegPath();

        Assert.Null(path);
    }

    [Fact]
    public void FindFfmpegPath_WithEnvVarPointingAtRealFile_ReturnsThatPath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "fake_ffmpeg_" + Guid.NewGuid() + ".exe");
        File.WriteAllText(tempFile, "not a real binary");

        try
        {
            Environment.SetEnvironmentVariable("MANDARIN_FFMPEG_PATH", tempFile);

            var path = FfmpegLocator.FindFfmpegPath();

            Assert.Equal(tempFile, path);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
