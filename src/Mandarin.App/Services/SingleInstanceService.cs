using System.IO;
using System.IO.Pipes;

namespace Mandarin.App.Services;

/// <summary>
/// Ensures only one Mandarin instance runs at a time. When the Explorer "Convert with
/// Mandarin" verb launches a second instance with a file path argument, that second
/// instance forwards the path to the already-running one over a local named pipe and
/// exits immediately, instead of opening a second tray icon/panel.
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\Mandarin.SingleInstance";
    private const string PipeName = "Mandarin.FileHandoff";

    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCancellation;

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return createdNew;
    }

    public static bool TrySendToRunningInstance(string filePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(filePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void StartListening(Action<string> onFileReceived)
    {
        _listenerCancellation = new CancellationTokenSource();
        var token = _listenerCancellation.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server);
                    var line = await reader.ReadLineAsync(token);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        onFileReceived(line);
                    }
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException)
                {
                    // Pipe torn down or listener stopping; loop will exit via the token check.
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _listenerCancellation?.Cancel();
        _listenerCancellation?.Dispose();
        _mutex?.Dispose();
    }
}
