using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;
using SharpCompressZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace Mandarin.Core.Conversion.Archives;

/// <summary>
/// Extracts ZIP/TAR/GZIP/RAR archives and creates ZIP/TAR/GZIP archives from any single file OR folder.
/// </summary>
public sealed class ArchiveConverter : IConverter
{
    private const string ExtractTarget = "extract";

    private static readonly HashSet<string> ArchiveExtensions = new() { "zip", "tar", "gz", "tgz", "rar" };
    private static readonly HashSet<string> CreatableTargets = new() { "zip", "tar", "gz" };

    public IReadOnlySet<string> SourceExtensions { get; } =
        new HashSet<string>(ArchiveExtensions) { "*", "folder", "" };

    public IReadOnlySet<string> TargetExtensions { get; } =
        new HashSet<string>(CreatableTargets) { ExtractTarget };

    public bool CanConvert(string sourceExtension, string targetExtension)
    {
        if (targetExtension == ExtractTarget)
        {
            return ArchiveExtensions.Contains(sourceExtension);
        }

        return CreatableTargets.Contains(targetExtension);
    }

    public Task<ConversionResult> ConvertAsync(
        string sourcePath,
        string targetPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                var targetExtension = FileExtensions.GetNormalizedExtension(targetPath);
                var result = targetExtension == ExtractTarget
                    ? Extract(sourcePath, cancellationToken)
                    : Create(sourcePath, targetPath, targetExtension);

                progress?.Report(1);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ConversionResult.Failed($"Could not convert '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }

    private static ConversionResult Extract(string sourcePath, CancellationToken cancellationToken)
    {
        var outputDirectory = FileExtensions.BuildOutputDirectoryPath(sourcePath);
        Directory.CreateDirectory(outputDirectory);

        using var reader = ReaderFactory.OpenReader(sourcePath, new ReaderOptions());
        while (reader.MoveToNextEntry())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            var entryName = string.IsNullOrEmpty(reader.Entry.Key)
                ? Path.GetFileNameWithoutExtension(sourcePath)
                : reader.Entry.Key.Replace('/', Path.DirectorySeparatorChar);

            var destinationPath = Path.Combine(outputDirectory, entryName);
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            reader.WriteEntryToFile(destinationPath, new ExtractionOptions { Overwrite = true });
        }

        return ConversionResult.Succeeded(outputDirectory);
    }

    private static ConversionResult Create(string sourcePath, string targetPath, string targetExtension)
    {
        switch (targetExtension)
        {
            case "gz":
                CreateGzip(sourcePath, targetPath);
                break;
            case "zip":
                CreateZip(sourcePath, targetPath);
                break;
            case "tar":
                CreateTar(sourcePath, targetPath);
                break;
            default:
                return ConversionResult.Failed($"Unsupported archive target '.{targetExtension}'.");
        }

        return ConversionResult.Succeeded(targetPath);
    }

    private static void CreateGzip(string sourcePath, string targetPath)
    {
        if (Directory.Exists(sourcePath))
        {
            var tempTar = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tar");
            try
            {
                CreateTar(sourcePath, tempTar);
                using var input = File.OpenRead(tempTar);
                using var output = File.Create(targetPath);
                using var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal);
                input.CopyTo(gzip);
            }
            finally
            {
                if (File.Exists(tempTar)) File.Delete(tempTar);
            }
        }
        else
        {
            using var input = File.OpenRead(sourcePath);
            using var output = File.Create(targetPath);
            using var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal);
            input.CopyTo(gzip);
        }
    }

    private static void CreateZip(string sourcePath, string targetPath)
    {
        if (Directory.Exists(sourcePath))
        {
            if (File.Exists(targetPath)) File.Delete(targetPath);
            ZipFile.CreateFromDirectory(sourcePath, targetPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        else
        {
            using var archive = SharpCompressZipArchive.CreateArchive();
            using var input = File.OpenRead(sourcePath);
            archive.AddEntry(Path.GetFileName(sourcePath), input, closeStream: false, input.Length, null);

            using var output = File.Create(targetPath);
            archive.SaveTo(output, new ZipWriterOptions(SharpCompress.Common.CompressionType.Deflate));
        }
    }

    private static void CreateTar(string sourcePath, string targetPath)
    {
        using var archive = TarArchive.CreateArchive();
        var openStreams = new List<FileStream>();

        try
        {
            if (Directory.Exists(sourcePath))
            {
                AddDirectoryToTar(archive, sourcePath, "", openStreams);
            }
            else
            {
                var input = File.OpenRead(sourcePath);
                openStreams.Add(input);
                archive.AddEntry(Path.GetFileName(sourcePath), input, closeStream: false, input.Length, null);
            }

            using var output = File.Create(targetPath);
            archive.SaveTo(output, new TarWriterOptions(SharpCompress.Common.CompressionType.None, finalizeArchiveOnClose: true));
        }
        finally
        {
            foreach (var stream in openStreams)
            {
                stream.Dispose();
            }
        }
    }

    private static void AddDirectoryToTar(IWritableArchive archive, string rootDirectory, string relativePath, List<FileStream> openStreams)
    {
        foreach (var file in Directory.GetFiles(rootDirectory))
        {
            var fileName = Path.GetFileName(file);
            var entryName = string.IsNullOrEmpty(relativePath) ? fileName : Path.Combine(relativePath, fileName).Replace('\\', '/');
            var input = File.OpenRead(file);
            openStreams.Add(input);
            archive.AddEntry(entryName, input, closeStream: false, input.Length, null);
        }

        foreach (var dir in Directory.GetDirectories(rootDirectory))
        {
            var dirName = Path.GetFileName(dir);
            var subRelative = string.IsNullOrEmpty(relativePath) ? dirName : Path.Combine(relativePath, dirName);
            AddDirectoryToTar(archive, dir, subRelative, openStreams);
        }
    }
}
