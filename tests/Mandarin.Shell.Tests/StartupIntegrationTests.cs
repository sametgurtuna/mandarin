using Mandarin.Shell;
using Microsoft.Win32;

namespace Mandarin.Shell.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class StartupIntegrationTests : IDisposable
{
    public StartupIntegrationTests() => StartupIntegration.Unregister();

    public void Dispose() => StartupIntegration.Unregister();

    [Fact]
    public void IsRegistered_BeforeRegistering_ReturnsFalse()
    {
        Assert.False(StartupIntegration.IsRegistered());
    }

    [Fact]
    public void Register_ThenIsRegistered_ReturnsTrue()
    {
        StartupIntegration.Register(@"C:\fake\Mandarin.App.exe");

        Assert.True(StartupIntegration.IsRegistered());
    }

    [Fact]
    public void Register_WritesQuotedExePathWithStartupFlag()
    {
        StartupIntegration.Register(@"C:\fake\Mandarin.App.exe");

        using var runKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run");

        Assert.NotNull(runKey);
        var value = (string)runKey!.GetValue("Mandarin")!;
        Assert.Contains("\"C:\\fake\\Mandarin.App.exe\"", value);
        Assert.Contains("--startup", value);
    }

    [Fact]
    public void Unregister_AfterRegistering_RemovesValue()
    {
        StartupIntegration.Register(@"C:\fake\Mandarin.App.exe");
        StartupIntegration.Unregister();

        Assert.False(StartupIntegration.IsRegistered());
    }

    [Fact]
    public void Unregister_WhenNeverRegistered_DoesNotThrow()
    {
        var exception = Record.Exception(StartupIntegration.Unregister);

        Assert.Null(exception);
    }
}
