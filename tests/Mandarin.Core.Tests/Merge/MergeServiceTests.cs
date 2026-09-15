using Mandarin.Core.Conversion.VideoAudio;
using Mandarin.Core.Merge;

namespace Mandarin.Core.Tests.Merge;

public class MergeServiceTests
{
    private sealed class FakeFfmpegProcessRunner : IFfmpegProcessRunner
    {
        public Task<FfmpegRunResult> RunAsync(IReadOnlyList<string> arguments, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            progress?.Report(1);
            return Task.FromResult(FfmpegRunResult.Succeeded());
        }
    }

    private static MergeService CreateService() => new(new IMergeTool[]
    {
        new PdfMergeTool(),
        new MediaMergeTool(new FakeFfmpegProcessRunner()),
    });

    [Fact]
    public void CanMerge_TwoPdfs_ReturnsTrue()
    {
        var service = CreateService();

        Assert.True(service.CanMerge(new[] { @"C:\a.pdf", @"C:\b.pdf" }));
    }

    [Fact]
    public void CanMerge_SingleFile_ReturnsFalse()
    {
        var service = CreateService();

        Assert.False(service.CanMerge(new[] { @"C:\a.pdf" }));
    }

    [Fact]
    public void CanMerge_MixedExtensions_ReturnsFalse()
    {
        var service = CreateService();

        Assert.False(service.CanMerge(new[] { @"C:\a.pdf", @"C:\b.mp4" }));
    }

    [Fact]
    public void CanMerge_UnsupportedExtension_ReturnsFalse()
    {
        var service = CreateService();

        Assert.False(service.CanMerge(new[] { @"C:\a.txt", @"C:\b.txt" }));
    }

    [Fact]
    public async Task MergeAsync_WhenCannotMerge_ReturnsFailureWithoutThrowing()
    {
        var service = CreateService();

        var result = await service.MergeAsync(new[] { @"C:\a.txt", @"C:\b.txt" }, null, CancellationToken.None);

        Assert.False(result.Success);
    }
}
