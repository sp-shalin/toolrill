using System.Diagnostics;
using System.Text.Json;
using ToolRill.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ToolRill.UnitTests;

[CollectionDefinition("Console diagnostics", DisableParallelization = true)]
public sealed class ConsoleDiagnosticsCollection;

[Collection("Console diagnostics")]
public sealed class McpDiagnosticsTests
{
    [Theory]
    [InlineData("Authorization: Bearer sensitive-value")]
    [InlineData("Proxy-Authorization: Basic sensitive-value")]
    [InlineData("{\"Authorization\":\"Bearer sensitive-value\"}")]
    [InlineData("X-Api-Key: sensitive-value")]
    [InlineData("api_key=sensitive-value")]
    [InlineData("API key: sensitive-value")]
    [InlineData("Cookie: session=sensitive-value; other=sensitive-value")]
    [InlineData("Set-Cookie: session=sensitive-value; HttpOnly")]
    [InlineData("Authorization: Digest username=user, response=sensitive-value")]
    [InlineData("password='sensitive-value with spaces'")]
    [InlineData("Password=first sensitive-value;Database=sample")]
    [InlineData("{\"password\":\"sensitive-value with spaces\"}")]
    [InlineData("{\"password\":\"prefix\\\"sensitive-value\"}")]
    [InlineData("{\"password\":\"prefix\\\\sensitive-value\"}")]
    [InlineData("client_secret=sensitive-value")]
    [InlineData("access_token=sensitive-value")]
    [InlineData("Request failed with Bearer sensitive-value")]
    [InlineData("https://user:sensitive-value@example.com/mcp")]
    [InlineData("https://example.com/mcp?api_key=sensitive-value&mode=private")]
    [InlineData("https://example.com/mcp?custom=sensitive-value#sensitive-value")]
    public void SensitiveStringsAreRedacted(string message)
    {
        var result = McpLogRedactor.Redact(message);
        Assert.DoesNotContain("sensitive-value", result);
        Assert.Contains("[REDACTED]", result);
        Assert.Equal(result, McpLogRedactor.Redact(result));
    }

    [Fact]
    public void OrdinaryMessageIsPreserved()
        => Assert.Equal("Connected to local. Count: 3.",
            McpLogRedactor.Redact("Connected to local. Count: 3."));

    [Fact]
    public void UrlStructureAndMultipleCredentialsAreHandled()
    {
        var result = McpLogRedactor.Redact(
            "https://user:password@example.com/mcp?key=first&other=second password=third");
        Assert.Equal("https://[REDACTED]@example.com/mcp?key=[REDACTED]&other=[REDACTED] password=[REDACTED]", result);
    }

    [Fact]
    public void EnabledDiagnosticsReserveStdoutAndEmitStructuredRedactedStderr()
    {
        CaptureConsole((stdout, stderr) =>
        {
            using var activity = new Activity("diagnostics-test")
                .SetIdFormat(ActivityIdFormat.W3C).Start();
            var diagnostics = new McpDiagnostics(new() { Enabled = true });
            diagnostics.ServerError("password=server-secret",
                "Failed\nAuthorization: Bearer message-secret");

            Assert.Empty(stdout.ToString());
            var output = stderr.ToString();
            Assert.DoesNotContain("server-secret", output);
            Assert.DoesNotContain("message-secret", output);
            using var document = JsonDocument.Parse(output);
            var entry = document.RootElement;
            Assert.Equal("ERROR", entry.GetProperty("level").GetString());
            Assert.Equal("password=[REDACTED]", entry.GetProperty("server").GetString());
            Assert.Contains("[REDACTED]", entry.GetProperty("message").GetString()!);
            Assert.Equal(activity.TraceId.ToString(), entry.GetProperty("traceId").GetString());
            Assert.Equal(activity.SpanId.ToString(), entry.GetProperty("spanId").GetString());
            Assert.Single(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        });
    }

    [Fact]
    public void ExceptionErrorOutputIsRedactedAndNeverUsesStdout()
    {
        CaptureConsole((stdout, stderr) =>
        {
            var exception = new InvalidOperationException(
                "Request https://example.com/mcp?token=url-secret failed: password=error-secret");
            McpLogRedactor.WriteError($"Error: {exception.Message}");
            Assert.Empty(stdout.ToString());
            Assert.DoesNotContain("url-secret", stderr.ToString());
            Assert.DoesNotContain("error-secret", stderr.ToString());
            Assert.Contains("[REDACTED]", stderr.ToString());
        });
    }

    [Fact]
    public void FrameworkLogsRedactFormattedMessagesAndNestedExceptions()
    {
        CaptureConsole((stdout, stderr) =>
        {
            using var provider = new McpDiagnosticLoggerProvider(new McpDiagnostics(new()));
            var logger = provider.CreateLogger("password=category-secret");
            var exception = new InvalidOperationException("Authorization: Bearer outer-secret",
                new Exception("https://example.com/?custom=inner-secret"));
            logger.LogError(new EventId(42), exception, "Call failed: {Credential}", "api_key=state-secret");

            Assert.Empty(stdout.ToString());
            var output = stderr.ToString();
            Assert.DoesNotContain("category-secret", output);
            Assert.DoesNotContain("outer-secret", output);
            Assert.DoesNotContain("inner-secret", output);
            Assert.DoesNotContain("state-secret", output);
            using var document = JsonDocument.Parse(output);
            Assert.Equal(42, document.RootElement.GetProperty("eventId").GetInt32());
            Assert.Contains("[REDACTED]", document.RootElement.GetProperty("exception").GetString()!);
        });
    }

    [Theory]
    [InlineData(false, "INFO", 0)]
    [InlineData(true, "ERROR", 1)]
    [InlineData(true, "WARN", 2)]
    [InlineData(true, "INFO", 3)]
    public void EnabledAndLevelSettingsAreRespected(bool enabled, string level, int expected)
    {
        CaptureConsole((stdout, stderr) =>
        {
            var diagnostics = new McpDiagnostics(new() { Enabled = enabled, Level = level });
            diagnostics.Info("Connected.");
            diagnostics.Warning("Retrying.");
            diagnostics.Error("Failed.");
            Assert.Empty(stdout.ToString());
            Assert.Equal(expected, stderr.ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        });
    }

    private static void CaptureConsole(Action<StringWriter, StringWriter> assertion)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            assertion(stdout, stderr);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}