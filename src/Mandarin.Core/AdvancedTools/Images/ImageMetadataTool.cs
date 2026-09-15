using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using Mandarin.Core.Conversion;

namespace Mandarin.Core.AdvancedTools.Images;

public sealed record MetadataTag(string Category, string Name, string Value, string RawKey);

/// <summary>
/// Inspects and selectively edits or removes metadata tags from images, matching Tangerine's Edit Metadata.
/// </summary>
public sealed class ImageMetadataTool : IAdvancedTool
{
    public AdvancedToolKind Kind => AdvancedToolKind.EditMetadata;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>
    {
        "jpg", "jpeg", "png", "webp", "tiff", "tif", "avif", "bmp", "heic"
    };

    public static IReadOnlyList<MetadataTag> ReadTags(string filePath)
    {
        var tags = new List<MetadataTag>();
        try
        {
            using var image = new MagickImage(filePath);

            // File basic metadata
            var fileInfo = new FileInfo(filePath);
            tags.Add(new MetadataTag("File", "File Size", $"{fileInfo.Length / 1024.0:F1} KB", "FileSize"));
            tags.Add(new MetadataTag("File", "Image Width", $"{image.Width} px", "Width"));
            tags.Add(new MetadataTag("File", "Image Height", $"{image.Height} px", "Height"));
            tags.Add(new MetadataTag("File", "Color Space", image.ColorSpace.ToString(), "ColorSpace"));
            tags.Add(new MetadataTag("File", "Depth", $"{image.Depth} bits", "Depth"));

            var exif = image.GetExifProfile();
            if (exif != null)
            {
                foreach (var val in exif.Values)
                {
                    string tagName = val.Tag.ToString();
                    string category = "ExifIFD";
                    if (tagName.StartsWith("GPS", StringComparison.OrdinalIgnoreCase))
                    {
                        category = "GPS";
                    }
                    else if (tagName is "Make" or "Model" or "Software" or "Artist" or "Copyright" or "DateTime" or "DateTimeOriginal" or "LensModel" or "FocalLength" or "ExposureTime" or "FNumber" or "ISOSpeedRatings")
                    {
                        category = "Camera";
                    }

                    tags.Add(new MetadataTag(category, tagName, val.GetValue()?.ToString() ?? "", tagName));
                }
            }

            var iptc = image.GetIptcProfile();
            if (iptc != null)
            {
                foreach (var val in iptc.Values)
                {
                    tags.Add(new MetadataTag("IPTC", val.Tag.ToString(), val.Value ?? "", val.Tag.ToString()));
                }
            }
        }
        catch
        {
        }

        return tags;
    }

    public Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var metaOptions = options as MetadataEditOptions ?? new MetadataEditOptions(RemoveAll: true);

        return Task.Run(() =>
        {
            try
            {
                progress?.Report(0);

                using var image = new MagickImage(sourcePath);
                cancellationToken.ThrowIfCancellationRequested();

                if (metaOptions.RemoveAll)
                {
                    image.Strip();
                }
                else if (metaOptions.RemovedTagNames != null && metaOptions.RemovedTagNames.Count > 0)
                {
                    var exif = image.GetExifProfile();
                    if (exif != null)
                    {
                        var toRemove = exif.Values
                            .Where(v => metaOptions.RemovedTagNames.Contains(v.Tag.ToString()))
                            .Select(v => v.Tag)
                            .ToList();

                        foreach (var tag in toRemove)
                        {
                            exif.RemoveValue(tag);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.EditMetadata));
                image.Write(targetPath);

                progress?.Report(1);
                return ConversionResult.Succeeded(targetPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ConversionResult.Failed($"Could not update metadata for '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }, cancellationToken);
    }
}
