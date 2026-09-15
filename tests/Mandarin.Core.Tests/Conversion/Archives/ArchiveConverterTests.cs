using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.Archives;
using Xunit;

namespace Mandarin.Core.Tests.Conversion.Archives;

public class ArchiveConverterTests : IDisposable
{
    private readonly string _tempDirectory;

    public ArchiveConverterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MandarinTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

    private string CreateTestFile(string name = "notes.txt", string content = "hello mandarin")
    {
        var path = Path.Combine(_tempDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Theory]
    [InlineData("zip")]
    [InlineData("tar")]
    [InlineData("gz")]
    public async Task ConvertAsync_CreateThenExtract_RoundTripsFileContent(string archiveExtension)
    {
        var sourcePath = CreateTestFile();
        var archivePath = Path.Combine(_tempDirectory, $"notes.{archiveExtension}");
        var converter = new ArchiveConverter();

        var createResult = await converter.ConvertAsync(sourcePath, archivePath, null, CancellationToken.None);
        Assert.True(createResult.Success);
        Assert.True(File.Exists(archivePath));

        var extractTargetPath = Path.Combine(_tempDirectory, $"unused.extract");
        var extractResult = await converter.ConvertAsync(archivePath, extractTargetPath, null, CancellationToken.None);

        Assert.True(extractResult.Success);
        Assert.True(Directory.Exists(extractResult.OutputPath));

        var extractedFiles = Directory.GetFiles(extractResult.OutputPath!, "*", SearchOption.AllDirectories);
        Assert.Single(extractedFiles);
        Assert.Equal("hello mandarin", File.ReadAllText(extractedFiles[0]));
    }

    [Fact]
    public async Task ConvertAsync_DirectoryToZip_CreatesValidZipAndExtracts()
    {
        var folderPath = Path.Combine(_tempDirectory, "myfolder");
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(Path.Combine(folderPath, "sub.txt"), "folder content");

        var zipPath = Path.Combine(_tempDirectory, "myfolder.zip");
        var converter = new ArchiveConverter();

        var createResult = await converter.ConvertAsync(folderPath, zipPath, null, CancellationToken.None);
        Assert.True(createResult.Success);
        Assert.True(File.Exists(zipPath));

        var extractTargetPath = Path.Combine(_tempDirectory, "unused.extract");
        var extractResult = await converter.ConvertAsync(zipPath, extractTargetPath, null, CancellationToken.None);
        Assert.True(extractResult.Success);

        var extractedFiles = Directory.GetFiles(extractResult.OutputPath!, "sub.txt", SearchOption.AllDirectories);
        Assert.Single(extractedFiles);
        Assert.Equal("folder content", File.ReadAllText(extractedFiles[0]));
    }

    [Fact]
    public void CanConvert_AnyArbitraryExtension_CanTargetZip()
    {
        var converter = new ArchiveConverter();

        Assert.True(converter.CanConvert("docx", "zip"));
        Assert.True(converter.CanConvert("someweirdext", "tar"));
        Assert.True(converter.CanConvert("folder", "zip"));
    }

    [Fact]
    public void CanConvert_RarSource_SupportsExtract()
    {
        var converter = new ArchiveConverter();

        Assert.True(converter.CanConvert("rar", "extract"));
    }

    [Fact]
    public void CanConvert_NeverOffersRarAsACreationTarget()
    {
        var converter = new ArchiveConverter();

        Assert.False(converter.CanConvert("txt", "rar"));
        Assert.DoesNotContain("rar", converter.TargetExtensions);
    }

    [Fact]
    public void CanConvert_NonArchiveSource_DoesNotOfferExtract()
    {
        var converter = new ArchiveConverter();

        Assert.False(converter.CanConvert("docx", "extract"));
    }

    [Fact]
    public void Registry_ForArbitraryFile_OffersArchiveTargets()
    {
        var registry = new ConverterRegistry(new IConverter[] { new ArchiveConverter() });

        var targets = registry.GetAvailableTargets("docx");

        Assert.Contains("zip", targets);
        Assert.Contains("tar", targets);
        Assert.Contains("gz", targets);
        Assert.DoesNotContain("extract", targets);
    }

    [Fact]
    public void Registry_ForZipFile_OffersExtractAndArchiveTargetsButNotSelf()
    {
        var registry = new ConverterRegistry(new IConverter[] { new ArchiveConverter() });

        var targets = registry.GetAvailableTargets("zip");

        Assert.Contains("extract", targets);
        Assert.Contains("tar", targets);
        Assert.DoesNotContain("zip", targets);
    }
}
