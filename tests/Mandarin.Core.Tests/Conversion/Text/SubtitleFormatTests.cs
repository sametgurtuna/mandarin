using Mandarin.Core.Conversion.Text;

namespace Mandarin.Core.Tests.Conversion.Text;

public class SubtitleFormatTests
{
    private const string SampleSrt =
        "1\n00:00:01,000 --> 00:00:04,500\nHello there\n\n2\n00:00:05,000 --> 00:00:07,250\nGeneral Kenobi\n";

    private const string SampleVtt =
        "WEBVTT\n\n00:00:01.000 --> 00:00:04.500\nHello there\n\n00:00:05.000 --> 00:00:07.250\nGeneral Kenobi\n";

    [Fact]
    public void ParseSrt_ReadsCuesWithCorrectTiming()
    {
        var cues = SubtitleFormat.ParseSrt(SampleSrt);

        Assert.Equal(2, cues.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), cues[0].Start);
        Assert.Equal(TimeSpan.FromMilliseconds(4500), cues[0].End);
        Assert.Equal("Hello there", cues[0].Text);
        Assert.Equal("General Kenobi", cues[1].Text);
    }

    [Fact]
    public void ParseVtt_ReadsCuesWithCorrectTiming()
    {
        var cues = SubtitleFormat.ParseVtt(SampleVtt);

        Assert.Equal(2, cues.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), cues[0].Start);
        Assert.Equal("Hello there", cues[0].Text);
    }

    [Fact]
    public void SrtRoundTripsThroughVtt()
    {
        var cues = SubtitleFormat.ParseSrt(SampleSrt);
        var vtt = SubtitleFormat.ToVtt(cues);
        var reparsed = SubtitleFormat.ParseVtt(vtt);

        Assert.Equal(cues.Count, reparsed.Count);
        Assert.Equal(cues[0].Start, reparsed[0].Start);
        Assert.Equal(cues[0].Text, reparsed[0].Text);
    }

    [Fact]
    public void ToPlainText_StripsTimingAndKeepsDialogue()
    {
        var cues = SubtitleFormat.ParseSrt(SampleSrt);

        var text = SubtitleFormat.ToPlainText(cues);

        Assert.Contains("Hello there", text);
        Assert.Contains("General Kenobi", text);
        Assert.DoesNotContain("-->", text);
    }

    [Fact]
    public void FromPlainText_AssignsSequentialEvenlySpacedTimestamps()
    {
        var cues = SubtitleFormat.FromPlainText("Line one\nLine two\n\nLine three");

        Assert.Equal(3, cues.Count);
        Assert.Equal(TimeSpan.Zero, cues[0].Start);
        Assert.Equal(cues[0].End, cues[1].Start);
        Assert.Equal(cues[1].End, cues[2].Start);
    }
}
