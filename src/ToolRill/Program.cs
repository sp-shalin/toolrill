using System.Runtime.InteropServices;
using ToolRill.Cli.Services;
using ToolRill.Core;

using var cancellationTokenSource = new CancellationTokenSource();
using var terminationSignal = OperatingSystem.IsLinux()
    ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
    {
        context.Cancel = true;
        cancellationTokenSource.Cancel();
    })
    : null;

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

var cancellationToken = cancellationTokenSource.Token;

try
{
    await CliCommandDispatcher.ExecuteAsync(args, cancellationToken);
}
catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
{
    // Normal shutdown.
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("ToolRill command failed.");
    McpLogRedactor.WriteError($"Error: {ex.Message}");

    Environment.ExitCode = 1;
}
