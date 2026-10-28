using ToolRill.Client;
using ToolRill.Core;
using Xunit;

namespace ToolRill.UnitTests;

public sealed class McpConfigurationValidatorTests
{
    [Theory]
    [InlineData("stdio")]
    [InlineData("STDIO")]
    [InlineData("streamable-http")]
    [InlineData("STREAMABLE-HTTP")]
    public void ValidTransportConfigurationIsAccepted(string transport)
    {
        McpConfigurationValidator.Validate([new()
        {
            Name = "server",
            Transport = transport,
            Command = "dotnet",
            ProjectPath = "sample.csproj",
            Url = "http://127.0.0.1:5000/mcp"
        }]);
    }

    [Theory]
    [InlineData("", "dotnet", "sample.csproj", "", "must specify a transport")]
    [InlineData("unknown", "dotnet", "sample.csproj", "", "unsupported transport")]
    [InlineData("stdio", "", "sample.csproj", "", "requires a command")]
    [InlineData("streamable-http", "", "", "", "requires a url")]
    [InlineData("streamable-http", "", "", "relative/mcp", "invalid URL")]
    public void InvalidTransportSettingsAreRejected(string transport, string command,
        string projectPath, string url, string message)
    {
        var error = Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([new()
        {
            Name = "server", Transport = transport, Command = command, ProjectPath = projectPath, Url = url
        }]));
        Assert.Contains(message, error.Message);
    }

    [Fact]
    public void EmptyServerListIsRejected()
        => Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([]));

    [Theory]
    [InlineData("dotnet")]
    [InlineData("docker")]
    [InlineData("npx")]
    [InlineData("uvx")]
    [InlineData("/opt/mcp/server")]
    public void DirectStdioCommandsDoNotRequireProjects(string command)
    {
        var server = new McpServerOptions { Name = "direct", Command = command };
        McpConfigurationValidator.Validate([server]);
        server.Args = ["server path.dll", "--option", "value with spaces", "", "${UNCHANGED}"];
        McpConfigurationValidator.Validate([server]);
        var resolved = StdioConnectionSettings.Resolve(server.Name, command, null, server.Args);
        Assert.Equal(command, resolved.Command);
        Assert.Equal(server.Args, resolved.Arguments);
    }

    [Fact]
    public void ProjectStdioArgumentsAreUnchangedAndConflictingArgsAreRejected()
    {
        var server = new McpServerOptions { Name = "project", Command = "dotnet", ProjectPath = "a path/server.csproj" };
        var resolved = StdioConnectionSettings.Resolve(server.Name, server.Command, server.ProjectPath, server.Args);
        Assert.Equal(["run", "--project", server.ProjectPath], resolved.Arguments);
        server.Args = ["ignored"];
        var error = Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([server]));
        Assert.Contains("cannot combine projectPath with args", error.Message);
    }

    [Fact]
    public void StdioEnvironmentExpandsOnceWithoutMutatingConfigurationOrParentEnvironment()
    {
        var variable = "TEST_TOOLRILL_" + Guid.NewGuid().ToString("N");
        var childVariable = variable + "_CHILD";
        Environment.SetEnvironmentVariable(variable, "${NOT_RECURSIVE}");
        try
        {
            var placeholder = "${" + variable + "}";
            var server = new McpServerOptions
            {
                Name = "direct", Command = "server",
                Env = new() { [childVariable] = "prefix:" + placeholder + placeholder, ["EMPTY"] = "" }
            };
            McpConfigurationValidator.Validate([server]);
            var resolved = StdioConnectionSettings.Resolve(server.Name, server.Command, null, server.Args, server.Env);
            Assert.Equal("prefix:${NOT_RECURSIVE}${NOT_RECURSIVE}", resolved.EnvironmentVariables[childVariable]);
            Assert.Equal("", resolved.EnvironmentVariables["EMPTY"]);
            Assert.Equal("prefix:" + placeholder + placeholder, server.Env[childVariable]);
            Assert.Null(Environment.GetEnvironmentVariable(childVariable));
            Assert.Equal("[REDACTED]", McpLogRedactor.Redact("${NOT_RECURSIVE}"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingOrEmptyStdioEnvironmentVariablesFailOnlyForEnabledServers(string? value)
    {
        var variable = "TEST_TOOLRILL_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, value);
        try
        {
            var server = new McpServerOptions
            {
                Name = "direct", Command = "server", Env = new() { ["TOKEN"] = "${" + variable + "}" }
            };
            var error = Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([server]));
            Assert.Contains(variable, error.Message);
            Assert.Contains("missing or empty", error.Message);
            server.Enabled = false;
            McpConfigurationValidator.Validate([server]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("BAD=KEY", "value")]
    [InlineData("BAD\0KEY", "value")]
    [InlineData("KEY", "value\0bad")]
    [InlineData("KEY", null)]
    public void InvalidStdioEnvironmentEntriesAreRejected(string key, string? value)
        => Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([new()
        {
            Name = "direct", Command = "server", Env = new() { [key] = value! }
        }]));

    [Theory]
    [InlineData(null)]
    [InlineData("bad\0argument")]
    public void InvalidStdioArgumentsAreRejected(string? argument)
        => Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([new()
        {
            Name = "direct", Command = "server", Args = [argument!]
        }]));

    [Fact]
    public void HttpEnvironmentValuesExpandWithoutChangingConfiguration()
    {
        var variable = "TOOLRILL_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "segment");
        try
        {
            var placeholder = "${" + variable + "}";
            var server = new McpServerOptions
            {
                Name = "remote", Transport = "streamable-http",
                Url = "https://example.com/" + placeholder,
                Headers = new() { ["X-Custom"] = placeholder + "=" + placeholder }
            };
            McpConfigurationValidator.Validate([server]);
            var resolved = HttpConnectionSettings.Resolve(server.Name, server.Url, server.Headers);
            Assert.Equal("https://example.com/segment", resolved.Endpoint.AbsoluteUri);
            Assert.Equal("segment=segment", resolved.Headers["X-Custom"]);
            Assert.Equal(placeholder + "=" + placeholder, server.Headers["X-Custom"]);
            Assert.Contains(placeholder, server.Url);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, "")]
    [InlineData(true, "")]
    public void MissingOrEmptyHttpVariablesFailOnlyForEnabledServers(bool inHeader, string? value)
    {
        var variable = "TOOLRILL_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, value);
        try
        {
            var placeholder = "${" + variable + "}";
            var server = new McpServerOptions
            {
                Name = "remote", Transport = "streamable-http",
                Url = inHeader ? "https://example.com/mcp" : placeholder,
                Headers = inHeader ? new() { ["Authorization"] = "Bearer " + placeholder } : []
            };
            var error = Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([server]));
            Assert.Contains("remote", error.Message);
            Assert.Contains(variable, error.Message);
            Assert.Contains("missing or empty", error.Message);
            server.Enabled = false;
            McpConfigurationValidator.Validate([server]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Theory]
    [InlineData("Bad Header", "private-header-value")]
    [InlineData("X-Custom", "private-header-value\r\nInjected: yes")]
    [InlineData("Content-Type", "private-header-value")]
    public void InvalidHeadersAreRejectedWithoutEchoingValues(string name, string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([new()
        {
            Name = "remote", Transport = "streamable-http", Url = "https://example.com/mcp",
            Headers = new() { [name] = value }
        }]));
        Assert.Contains("HTTP header", error.Message);
        Assert.DoesNotContain(value, error.ToString());
    }

    [Fact]
    public void DuplicateHeadersAreRejectedIgnoringCase()
        => Assert.Throws<InvalidOperationException>(() => HttpConnectionSettings.Resolve(
            "remote", "https://example.com/mcp", new Dictionary<string, string>
            {
                ["X-Custom"] = "first-header-value", ["x-custom"] = "second-header-value"
            }));

    [Fact]
    public void ConfiguredCustomHeaderValuesAreRedactedWithoutCredentialLabels()
    {
        var secret = "custom-" + Guid.NewGuid().ToString("N");
        HttpConnectionSettings.Resolve("remote", "https://example.com/mcp", new Dictionary<string, string>
        {
            ["X-Unusual"] = secret
        });
        Assert.Equal("Failure: [REDACTED]", McpLogRedactor.Redact("Failure: " + secret));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void DisabledServersStillRequireNames(string name)
        => Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([
            new() { Name = name, Enabled = false }
        ]));

    [Fact]
    public void DuplicateNamesAreRejectedCaseInsensitivelyEvenWhenDisabled()
        => Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([
            new() { Name = "server", Enabled = false },
            new() { Name = "SERVER", Enabled = false }
        ]));

    [Fact]
    public void DisabledServersSkipTransportValidation()
        => McpConfigurationValidator.Validate([new() { Name = "disabled", Enabled = false, Transport = "unknown" }]);

    [Theory]
    [InlineData(0, 60)]
    [InlineData(-1, 60)]
    [InlineData(30, 0)]
    [InlineData(30, -1)]
    [InlineData(int.MaxValue, 60)]
    [InlineData(30, int.MaxValue)]
    public void OutOfRangeTimeoutsAreRejected(int connectionTimeout, int toolCallTimeout)
    {
        var error = Assert.Throws<InvalidOperationException>(() => McpConfigurationValidator.Validate([new()
        {
            Name = "server", Transport = "streamable-http", Url = "http://127.0.0.1:5000/mcp",
            ConnectionTimeoutSeconds = connectionTimeout, ToolCallTimeoutSeconds = toolCallTimeout
        }]));
        Assert.Contains("positive integers", error.Message);
    }

    [Fact]
    public void TimeoutDefaultsAreApplied()
    {
        var options = new McpServerOptions();
        Assert.Equal(30, options.ConnectionTimeoutSeconds);
        Assert.Equal(60, options.ToolCallTimeoutSeconds);
    }
}