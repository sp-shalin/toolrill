using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ToolRill.Core;

public sealed class McpDiagnostics
{
    private readonly McpDiagnosticsOptions _options;

    public McpDiagnostics(McpDiagnosticsOptions options)
    {
        _options = options;
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warning(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message)
    {
        Write("ERROR", message);
    }

    public void ServerInfo(string serverName, string message)
    {
        Write("INFO", message, serverName);
    }

    public void ServerWarning(string serverName, string message)
    {
        Write("WARN", message, serverName);
    }

    public void ServerError(string serverName, string message)
    {
        Write("ERROR", message, serverName);
    }

    internal void Log(LogLevel level, EventId eventId, string category, string message, Exception? exception)
    {
        var severity = level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            _ => null
        };

        if (severity is not null)
        {
            Write(severity, message, category: category, eventId: eventId.Id,
                exception: exception?.ToString());
        }
    }

    private void Write(string level, string message, string? serverName = null,
        string? category = null, int? eventId = null, string? exception = null)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (!ShouldLog(level))
        {
            return;
        }

        var activity = Activity.Current;
        var entry = new
        {
            timestamp = DateTimeOffset.UtcNow,
            level,
            server = serverName is null ? null : McpLogRedactor.Redact(serverName),
            message = McpLogRedactor.Redact(message),
            category = category is null ? null : McpLogRedactor.Redact(category),
            eventId,
            exception = exception is null ? null : McpLogRedactor.Redact(exception),
            traceId = activity?.TraceId.ToString(),
            spanId = activity?.SpanId.ToString()
        };

        Console.Error.WriteLine(JsonSerializer.Serialize(entry));
    }

    private bool ShouldLog(string level)
    {
        return _options.Level.ToUpperInvariant() switch
        {
            "ERROR" => level is "ERROR" or "CRITICAL",
            "WARN" => level is "WARN" or "ERROR" or "CRITICAL",
            _ => level is "INFO" or "WARN" or "ERROR" or "CRITICAL"
        };
    }
}