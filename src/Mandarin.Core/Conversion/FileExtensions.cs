using System;
using System.IO;

namespace Mandarin.Core.Conversion;

public static class FileExtensions
{
    /// <summary>
    /// Lowercase file extension without the leading dot, e.g. "jpg" for "photo.JPG", or "folder" for directories.
    /// </summary>
    public static string GetNormalizedExtension(string filePath)
    {
        if (Directory.Exists(filePath))
        {
            return "folder";
        }

        var extension = Path.GetExtension(filePath);
        return extension.Length > 0 ? extension[1..].ToLowerInvariant() : string.Empty;
    }

    /// <summary>
    /// Builds an output path in the same directory as the source file or folder, with the new
    /// extension, appending " (2)", " (3)", etc. if a file already exists at that path.
    /// </summary>
    public static string BuildOutputPath(string sourcePath, string targetExtension)
    {
        var isDir = Directory.Exists(sourcePath);
        var trimmed = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var directory = Path.GetDirectoryName(trimmed) ?? string.Empty;
        var baseName = isDir ? Path.GetFileName(trimmed) : Path.GetFileNameWithoutExtension(sourcePath);

        var candidate = Path.Combine(directory, $"{baseName}.{targetExtension}");
        var counter = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} ({counter}).{targetExtension}");
            counter++;
        }

        return candidate;
    }

    /// <summary>
    /// Builds a non-clobbering output path in the same directory as the source file, with
    /// a descriptive suffix before the extension (e.g. "photo (cropped).jpg").
    /// </summary>
    public static string BuildSuffixedOutputPath(string sourcePath, string suffix, string? extensionOverride = null)
    {
        var isDir = Directory.Exists(sourcePath);
        var trimmed = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var directory = Path.GetDirectoryName(trimmed) ?? string.Empty;
        var baseName = isDir ? Path.GetFileName(trimmed) : Path.GetFileNameWithoutExtension(sourcePath);
        var extension = extensionOverride ?? GetNormalizedExtension(sourcePath);

        var candidate = Path.Combine(directory, $"{baseName} {suffix}.{extension}");
        var counter = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} {suffix} ({counter}).{extension}");
            counter++;
        }

        return candidate;
    }

    /// <summary>
    /// Builds a non-clobbering output directory path in the same folder as the source
    /// file, named after the source's base filename (e.g. "archive" for "archive.zip"),
    /// appending " (2)", " (3)", etc. if that folder already exists.
    /// </summary>
    public static string BuildOutputDirectoryPath(string sourcePath)
    {
        var trimmed = sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var directory = Path.GetDirectoryName(trimmed) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(trimmed);

        var candidate = Path.Combine(directory, baseName);
        var counter = 2;
        while (Directory.Exists(candidate) || File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} ({counter})");
            counter++;
        }

        return candidate;
    }
}
