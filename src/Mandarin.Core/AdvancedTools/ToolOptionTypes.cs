using System;
using System.Collections.Generic;

namespace Mandarin.Core.AdvancedTools;

/// <summary>
/// How to shrink a file. <paramref name="Quality"/> (1 smallest … 100 best) is the
/// baseline every format understands; the rest are per-format refinements, each
/// null/false meaning "leave it alone".
/// </summary>
/// <param name="TargetSizeBytes">
/// When set, the tool aims to land under this size and treats Quality as the
/// starting point rather than the answer.
/// </param>
/// <param name="MaxLongEdge">Images: cap the longer side, in pixels. Never upscales.</param>
/// <param name="Lossless">Images: use the format's lossless mode (WebP, PNG).</param>
/// <param name="MaxVideoHeight">Video: cap the height, e.g. 720. Never upscales.</param>
/// <param name="AudioBitrateKbps">Video/audio: fixed audio bitrate instead of one derived from Quality.</param>
public sealed record CompressOptions(
    int Quality,
    long? TargetSizeBytes = null,
    int? MaxLongEdge = null,
    bool Lossless = false,
    int? MaxVideoHeight = null,
    int? AudioBitrateKbps = null) : IToolOptions;

/// <summary>A pixel rectangle in the source image/video frame's own coordinate space.</summary>
public sealed record CropOptions(int X, int Y, int Width, int Height) : IToolOptions;

public sealed record TrimOptions(TimeSpan Start, TimeSpan End) : IToolOptions;

/// <summary>1-based, inclusive page range.</summary>
public sealed record PdfSplitOptions(int FromPage, int ToPage) : IToolOptions;

public sealed record MediaSplitOptions(TimeSpan At) : IToolOptions;

/// <summary>Light, color, and detail adjustments for photo editing.</summary>
public sealed record ImageEditOptions(
    double Exposure = 0.0,
    double Brightness = 0.0,
    double Contrast = 0.0,
    double Highlights = 0.0,
    double Shadows = 0.0,
    double Gamma = 1.0,
    double Temperature = 0.0,
    double Tint = 0.0,
    double Saturation = 0.0,
    double Vibrance = 0.0,
    double Hue = 0.0,
    double Clarity = 0.0,
    double NoiseReduction = 0.0
) : IToolOptions;

/// <summary>Metadata inspection and removal options.</summary>
public sealed record MetadataEditOptions(
    bool RemoveAll = false,
    IReadOnlyList<string>? RemovedTagNames = null
) : IToolOptions;

public enum RedactStyle
{
    Solid,
    Blur,
    Pixelate
}

public sealed record RedactionRegion(
    double NormalizedX,
    double NormalizedY,
    double NormalizedWidth,
    double NormalizedHeight,
    RedactStyle Style = RedactStyle.Solid,
    string ColorHex = "#000000"
);

public sealed record RedactOptions(
    IReadOnlyList<RedactionRegion> Regions
) : IToolOptions;
