using Mandarin.Core.Conversion;

namespace Mandarin.Core.Tests.Conversion;

public class FileExtensionsTests : IDisposable
{
    private readonly string _tempDirectory;

    public FileExtensionsTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    [Fact]
    public void GetNormalizedExtension_LowercasesAndStripsDot()
    {
        Assert.Equal("jpg", FileExtensions.GetNormalizedExtension(@"C:\photos\cat.JPG"));
    }

    [Fact]
    public void BuildOutputPath_WhenTargetDoesNotExist_UsesPlainName()
    {
        var source = Path.Combine(_tempDirectory, "photo.png");

        var output = FileExtensions.BuildOutputPath(source, "jpg");

        Assert.Equal(Path.Combine(_tempDirectory, "photo.jpg"), output);
    }

    [Fact]
    public void BuildOutputPath_WhenTargetExists_AppendsCounter()
    {
        var source = Path.Combine(_tempDirectory, "photo.png");
        File.WriteAllText(Path.Combine(_tempDirectory, "photo.jpg"), "existing");

        var output = FileExtensions.BuildOutputPath(source, "jpg");

        Assert.Equal(Path.Combine(_tempDirectory, "photo (2).jpg"), output);
    }
}
