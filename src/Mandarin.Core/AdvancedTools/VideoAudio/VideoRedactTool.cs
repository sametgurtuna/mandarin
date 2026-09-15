using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mandarin.Core.Conversion;
using Mandarin.Core.Conversion.VideoAudio;

namespace Mandarin.Core.AdvancedTools.VideoAudio;

/// <summary>
/// Applies solid or blurred redaction boxes to video clips using FFmpeg.
/// </summary>
public sealed class VideoRedactTool : IAdvancedTool
{
    private readonly IFfmpegProcessRunner _runner;

    public AdvancedToolKind Kind => AdvancedToolKind.RedactVideo;

    public IReadOnlySet<string> SupportedExtensions { get; } = new HashSet<string>(MediaExtensions.VideoExtensions);

    public VideoRedactTool(IFfmpegProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<ConversionResult> ExecuteAsync(
        string sourcePath,
        IToolOptions? options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var redactOptions = options as RedactOptions ?? new RedactOptions(Array.Empty<RedactionRegion>());
        if (redactOptions.Regions.Count == 0)
        {
            return ConversionResult.Failed("No redaction areas specified.");
        }

        var probeInfo = await MediaProbe.ProbeAsync(sourcePath, cancellationToken);
        int vw = probeInfo?.VideoWidth ?? 1920;
        int vh = probeInfo?.VideoHeight ?? 1080;

        var targetPath = FileExtensions.BuildSuffixedOutputPath(sourcePath, ToolOutputNaming.SuffixFor(AdvancedToolKind.RedactPhoto));

        // Build filter string with evaluated safe integer bounds
        var filters = new List<string>();
        foreach (var r in redactOptions.Regions)
        {
            int rx = Math.Clamp((int)Math.Round(r.NormalizedX * vw), 0, Math.Max(0, vw - 2));
            int ry = Math.Clamp((int)Math.Round(r.NormalizedY * vh), 0, Math.Max(0, vh - 2));
            int rw = Math.Clamp((int)Math.Round(r.NormalizedWidth * vw), 2, Math.Max(2, vw - rx));
            int rh = Math.Clamp((int)Math.Round(r.NormalizedHeight * vh), 2, Math.Max(2, vh - ry));

            if (r.Style == RedactStyle.Solid)
            {
                filters.Add($"drawbox=x={rx}:y={ry}:w={rw}:h={rh}:color=black:t=fill");
            }
            else
            {
                filters.Add($"delogo=x={rx}:y={ry}:w={rw}:h={rh}");
            }
        }

        string vf = string.Join(",", filters);
        var args = new[]
        {
            "-y",
            "-i", sourcePath,
            "-vf", vf,
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-c:a", "copy",
            targetPath
        };

        var runResult = await _runner.RunAsync(args, progress, cancellationToken);
        if (!runResult.Success || !File.Exists(targetPath))
        {
            // Fallback without libx264 flags
            args = new[]
            {
                "-y",
                "-i", sourcePath,
                "-vf", vf,
                "-c:a", "copy",
                targetPath
            };
            runResult = await _runner.RunAsync(args, progress, cancellationToken);
        }

        if (!runResult.Success || !File.Exists(targetPath))
        {
            return ConversionResult.Failed(runResult.ErrorMessage ?? "Video redaction failed.");
        }

        return ConversionResult.Succeeded(targetPath);
    }
}
