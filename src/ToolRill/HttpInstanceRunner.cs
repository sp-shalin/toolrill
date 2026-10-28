using System.Diagnostics;
using ToolRill.Server;

namespace ToolRill.Cli;

public sealed class HttpInstanceRunner
{
    private readonly List<Process> _processes = [];

    public async Task StartAsync(
        IEnumerable<McpHttpInstance> instances,
        CancellationToken cancellationToken = default)
    {
        foreach (var instance in instances)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false
            };

            startInfo.ArgumentList.Add(
                typeof(HttpInstanceRunner).Assembly.Location);

            startInfo.ArgumentList.Add("serve-http");

            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add(instance.ConfigurationPath);

            startInfo.ArgumentList.Add("--host");
            startInfo.ArgumentList.Add(instance.Name);

            startInfo.ArgumentList.Add("--url");
            startInfo.ArgumentList.Add(instance.Url);

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    $"Could not start instance '{instance.Name}'.");

            _processes.Add(process);
        }

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        foreach (var process in _processes)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process.Dispose();
        }

        _processes.Clear();
    }
}