using Mandarin.Shell;
using Microsoft.Win32;

namespace Mandarin.Shell.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class ExplorerIntegrationTests : IDisposable
{
    public ExplorerIntegrationTests() => ExplorerIntegration.Unregister();

    public void Dispose() => ExplorerIntegration.Unregister();

    [Fact]
    public void IsRegistered_BeforeRegistering_ReturnsFalse()
    {
        Assert.False(ExplorerIntegration.IsRegistered());
    }

    [Fact]
    public void Register_ThenIsRegistered_ReturnsTrue()
    {
        ExplorerIntegration.Register(@"C:\fake\Mandarin.App.exe");

        Assert.True(ExplorerIntegration.IsRegistered());
    }

    [Fact]
    public void Register_WritesCommandPointingAtExeWithArgumentPlaceholder()
    {
        ExplorerIntegration.Register(@"C:\fake\Mandarin.App.exe");

        using var commandKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Classes\*\shell\Mandarin.ConvertWithMandarin\command");

        Assert.NotNull(commandKey);
        var command = (string)commandKey!.GetValue(string.Empty)!;
        Assert.Contains("Mandarin.App.exe", command);
        Assert.Contains("%1", command);
    }

    [Fact]
    public void Unregister_AfterRegistering_RemovesKey()
    {
        ExplorerIntegration.Register(@"C:\fake\Mandarin.App.exe");
        ExplorerIntegration.Unregister();

        Assert.False(ExplorerIntegration.IsRegistered());
    }

    [Fact]
    public void Unregister_WhenNeverRegistered_DoesNotThrow()
    {
        var exception = Record.Exception(ExplorerIntegration.Unregister);

        Assert.Null(exception);
    }
}
