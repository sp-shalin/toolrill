using System.Text.Json;
using System.Text.Json.Nodes;
using ToolRill.Cli.Services;
using ToolRill.Core;
using ModelContextProtocol.Client;

namespace ToolRill.Cli.Commands;

internal static class ServerCommand
{
	public static async Task AddAsync(
		string name,
		string transport,
		string? projectPath,
		string? command,
		string? url,
		string configPath,
		string hostName = CliConfiguration.DefaultHost,
		IReadOnlyList<string>? headers = null,
		string? args = null,
		bool discover = false,
		CancellationToken cancellationToken = default)
	{
		var entry = new JsonObject { ["name"] = name };

		switch (transport.ToLowerInvariant())
		{
			case "stdio":
				if (headers is { Count: > 0 })
				{
					throw new ArgumentException("--header is only supported for streamable-http.");
				}

				if (url is not null)
				{
					throw new ArgumentException("--url is only supported for streamable-http.");
				}

				if (string.IsNullOrWhiteSpace(projectPath) && string.IsNullOrWhiteSpace(command))
				{
					throw new ArgumentException("Stdio servers require --project <path> or --command <executable>.");
				}

				entry["transport"] = "stdio";
				entry["command"] = command ?? "dotnet";
				if (projectPath is not null)
				{
					entry["projectPath"] = projectPath;
				}
				if (args is not null)
				{
					try
					{
						var arguments = JsonSerializer.Deserialize<List<string>>(args)
							?? throw new JsonException();
						if (arguments.Any(argument => argument is null))
						{
							throw new JsonException();
						}
						entry["args"] = JsonSerializer.SerializeToNode(arguments);
					}
					catch (JsonException)
					{
						throw new ArgumentException("--args requires a JSON array of strings.");
					}
				}
				break;

			case "streamable-http":
				if (projectPath is not null || command is not null || args is not null)
				{
					throw new ArgumentException("--project, --command and --args are only supported for stdio.");
				}

				if (string.IsNullOrWhiteSpace(url))
				{
					throw new ArgumentException("HTTP servers require --url <absolute-url>.");
				}

				entry["transport"] = "streamable-http";
				entry["url"] = url;
				if (headers is { Count: > 0 })
				{
					var headerObject = new JsonObject();
					var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
					foreach (var header in headers)
					{
						var separator = header.IndexOf('=');
						if (separator <= 0 || !names.Add(header[..separator]))
						{
							throw new ArgumentException("--header requires KEY=VALUE with unique header names (ignoring case).");
						}
						headerObject[header[..separator]] = header[(separator + 1)..];
					}
					entry["headers"] = headerObject;
				}
				break;

			default:
				throw new ArgumentException(
					$"Unsupported transport '{transport}'. Use stdio or streamable-http.");
		}

		var document = CliConfiguration.ReadDocument(configPath);
		var servers = CliConfiguration.GetServers(document, hostName);

		servers.Add(entry);
		var options = CliConfiguration.ReadServers(servers);
		McpConfigurationValidator.Validate(options);
		if (discover)
		{
			var tools = await DiscoverAsync(options[^1], cancellationToken);
			SetToolIncludes(entry, tools.Select(tool => tool.Name));
		}
		cancellationToken.ThrowIfCancellationRequested();
		CliConfiguration.Save(configPath, document);

		Console.WriteLine($"Added server '{name}' to host '{hostName}'.");
	}

	public static async Task InspectAsync(
		string name,
		string configPath,
		string hostName = CliConfiguration.DefaultHost,
		bool sync = false,
		CancellationToken cancellationToken = default)
	{
		var configuration = CliConfiguration.Load(configPath);
		using var configurationLifetime = configuration as IDisposable;
		var server = CliConfiguration.GetHost(configuration, hostName).Outgoing!
			.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
			?? throw new ArgumentException($"Server '{name}' was not found in the configuration.");
		if (!server.Enabled)
		{
			throw new InvalidOperationException($"Server '{name}' is disabled; enable it before inspecting.");
		}
		McpConfigurationValidator.Validate([server]);

		JsonObject? document = null;
		JsonObject? entry = null;
		if (sync)
		{
			document = CliConfiguration.ReadDocument(configPath);
			entry = CliConfiguration.GetServers(document, hostName).OfType<JsonObject>()
				.SingleOrDefault(candidate => string.Equals(candidate["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase))
				?? throw new ArgumentException($"Server '{name}' was not found in the base configuration.");
			if (entry["tools"] is not null && entry["tools"] is not JsonObject)
			{
				throw new InvalidOperationException("The 'tools' setting must be a JSON object.");
			}
		}

		var tools = await DiscoverAsync(server, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (sync)
		{
			SetToolIncludes(entry!, tools.Select(tool => tool.Name));
			CliConfiguration.Save(configPath, document!);
		}
		foreach (var tool in tools)
		{
			Console.WriteLine($"{tool.Name}\t{tool.Description}");
		}
	}

	private static async Task<IReadOnlyList<McpClientTool>> DiscoverAsync(
		McpServerOptions server, CancellationToken cancellationToken)
	{
		await using var connection = new McpServerConnection(server.Name, new(new() { Enabled = false }));
		await connection.ConnectAsync(server, cancellationToken);
		return await connection.ListToolsAsync(cancellationToken);
	}

	internal static void SetToolIncludes(JsonObject entry, IEnumerable<string> names)
	{
		var tools = entry["tools"] as JsonObject;
		if (tools is null)
		{
			tools = new JsonObject();
			entry["tools"] = tools;
		}
		tools["include"] = JsonSerializer.SerializeToNode(names.ToArray());
	}

	public static void List(string configPath, string hostName = CliConfiguration.DefaultHost)
	{
		var configuration = CliConfiguration.Load(configPath);
		using var configurationLifetime = configuration as IDisposable;
		var servers = CliConfiguration.GetHost(configuration, hostName).Outgoing!;

		if (servers.Count == 0)
		{
			Console.WriteLine("No servers configured.");
			return;
		}

		foreach (var server in servers)
		{
			Console.WriteLine(
				$"{server.Name}\t{(server.Enabled ? "Enabled" : "Disabled")}\t{server.Transport}");
		}
	}

	public static void Remove(string name, string configPath, string hostName = CliConfiguration.DefaultHost)
	{
		var document = CliConfiguration.ReadDocument(configPath);
		var servers = CliConfiguration.GetServers(document, hostName);
		var options = CliConfiguration.ReadServers(servers);
		var index = options.FindIndex(server =>
			string.Equals(server.Name, name, StringComparison.OrdinalIgnoreCase));

		if (index < 0)
		{
			throw new ArgumentException($"Server '{name}' was not found in the base configuration.");
		}

		servers.RemoveAt(index);

		if (servers.Count > 0)
		{
			McpConfigurationValidator.Validate(CliConfiguration.ReadServers(servers));
		}

		CliConfiguration.Save(configPath, document);
		Console.WriteLine($"Removed server '{name}' from host '{hostName}'.");
	}
}
