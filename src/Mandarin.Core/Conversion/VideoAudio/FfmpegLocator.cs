namespace Mandarin.Core.Conversion.VideoAudio;

/// <summary>
/// Finds the bundled or system ffmpeg.exe.
/// </summary>
public static class FfmpegLocator
{
    /// <param name="configuredPath">
    /// A path the user picked (MandarinSettings.FfmpegPath). It wins over every
    /// automatic location, but is ignored if the file has since been moved.
    /// </param>
    public static string? FindFfmpegPath(string? configuredPath = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var envPath = Environment.GetEnvironmentVariable("MANDARIN_FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return File.Exists(envPath) ? envPath : null;
        }

        var nextToAppInFolder = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe");
        if (File.Exists(nextToAppInFolder))
        {
            return nextToAppInFolder;
        }

        var nextToApp = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(nextToApp))
        {
            return nextToApp;
        }

        // Search PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            try
            {
                var candidate = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
            }
        }

        // Search common WinGet / LocalAppData installation directories
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var wingetPackages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetPackages))
            {
                try
                {
                    var found = Directory.GetFiles(wingetPackages, "ffmpeg.exe", SearchOption.AllDirectories);
                    if (found.Length > 0)
                    {
                        return found[0];
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Confirms a candidate really is a working ffmpeg by running it with
    /// <c>-version</c>. Used by the "locate ffmpeg" flow so a wrong pick is
    /// caught while the user is still in the dialog.
    /// </summary>
    public static bool IsWorkingFfmpeg(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                ArgumentList = { "-version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }

                return false;
            }

            return process.ExitCode == 0 && output.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return false;
        }
    }
}
