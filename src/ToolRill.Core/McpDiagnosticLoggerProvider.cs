using Microsoft.Extensions.Logging;

namespace ToolRill.Core;

public sealed class McpDiagnosticLoggerProvider(McpDiagnostics diagnostics) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DiagnosticLogger(diagnostics, categoryName);

    public void Dispose()
    {
    }

    private sealed class DiagnosticLogger(McpDiagnostics diagnostics, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                diagnostics.Log(logLevel, eventId, category, formatter(state, exception), exception);
            }
        }
    }
}