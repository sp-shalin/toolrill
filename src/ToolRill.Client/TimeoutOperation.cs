namespace ToolRill.Client;

public static class TimeoutOperation
{
    public static async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await operation(linked.Token);
        }
        catch (OperationCanceledException exception) when (
            deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"MCP operation exceeded its {timeout.TotalSeconds}-second timeout.", exception);
        }
    }
}