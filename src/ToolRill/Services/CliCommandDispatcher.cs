using ToolRill.Cli.Commands;

namespace ToolRill.Cli.Services;

internal static class CliCommandDispatcher
{
	public static async Task ExecuteAsync(string[] args, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "start";

		if (IsHelp(command))
		{
			ParseOptions(args, 1);
			HelpCommand.Execute();
			return;
		}

		if (command == "help")
		{
			HelpCommand.Execute(args[1..]);
			return;
		}

		if (args.Skip(1).Any(IsHelp))
		{
			if ((command is "server" or "config")
				&& args.Length > 1
				&& !args[1].StartsWith('-'))
			{
				HelpCommand.Execute(command, args[1]);
			}
			else
			{
				HelpCommand.Execute(command);
			}

			return;
		}

		switch (command)
		{
			case "init":
			{
				var options = ParseOptions(args, 1, "--config");
				InitCommand.Execute(GetConfigPath(options));
				break;
			}

			case "server":
				await ExecuteServerAsync(args, cancellationToken);
				break;

			case "config":
				ExecuteConfig(args);
				break;

			case "start":
			case "stdio":
			case "serve-http":
			{
				var allowed = command switch
				{
					"start" => new[] { "--config", "--host", "--mode", "--url" },
					"serve-http" => new[] { "--config", "--host", "--url" },
					_ => new[] { "--config", "--host" }
				};
				var options = ParseOptions(args, args.Length == 0 ? 0 : 1, allowed);
				var mode = command == "start"
					? options.GetValueOrDefault("--mode")
					: command;

				await StartCommand.ExecuteAsync(
					mode,
					GetConfigPath(options),
					options.GetValueOrDefault("--url"),
					cancellationToken,
					GetHostName(options));
				break;
			}

			case "manager":
			{
				var options = ParseOptions(args, 1, "--config");
				await StartCommand.RunManagerAsync(
					GetConfigPath(options),
					cancellationToken);
				break;
			}

			default:
				throw new ArgumentException(
					$"Unknown command '{command}'. Run 'toolrill help' for usage.");
		}
	}

	private static async Task ExecuteServerAsync(string[] args, CancellationToken cancellationToken)
	{
		if (args.Length == 1)
		{
			HelpCommand.Execute("server");
			return;
		}

		switch (args[1].ToLowerInvariant())
		{
			case "add":
			{
				var name = GetServerName(args);
				var headers = new List<string>();
				var options = ParseOptions(
					args, 3, headers, "--config", "--host", "--transport", "--project", "--command", "--url", "--header", "--args", "--discover");
				var transport = options.GetValueOrDefault("--transport")
					?? throw new ArgumentException("Server add requires --transport <transport>.");

				await ServerCommand.AddAsync(
					name,
					transport,
					options.GetValueOrDefault("--project"),
					options.GetValueOrDefault("--command"),
					options.GetValueOrDefault("--url"),
					GetConfigPath(options),
					GetHostName(options),
					headers,
					options.GetValueOrDefault("--args"),
					options.ContainsKey("--discover"),
					cancellationToken);
				break;
			}

			case "inspect":
			{
				var name = GetServerName(args);
				var options = ParseOptions(args, 3, "--config", "--host", "--sync");
				await ServerCommand.InspectAsync(name, GetConfigPath(options), GetHostName(options),
					options.ContainsKey("--sync"), cancellationToken);
				break;
			}

			case "list":
			{
				var options = ParseOptions(args, 2, "--config", "--host");
				ServerCommand.List(GetConfigPath(options), GetHostName(options));
				break;
			}

			case "remove":
			{
				var name = GetServerName(args);
				var options = ParseOptions(args, 3, "--config", "--host");
				ServerCommand.Remove(name, GetConfigPath(options), GetHostName(options));
				break;
			}

			default:
				throw new ArgumentException(
					$"Unknown server command '{args[1]}'. Run 'toolrill help server' for usage.");
		}
	}

	private static void ExecuteConfig(string[] args)
	{
		if (args.Length == 1)
		{
			HelpCommand.Execute("config");
			return;
		}

		if (!args[1].Equals("validate", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException(
				$"Unknown config command '{args[1]}'. Run 'toolrill help config' for usage.");
		}

		var options = ParseOptions(args, 2, "--config", "--host");
		ConfigCommand.Validate(GetConfigPath(options), options.GetValueOrDefault("--host"));
	}

	private static Dictionary<string, string> ParseOptions(
		string[] args,
		int startIndex,
		params string[] allowedOptions)
		=> ParseOptions(args, startIndex, null, allowedOptions);

	private static Dictionary<string, string> ParseOptions(
		string[] args,
		int startIndex,
		List<string>? headers,
		params string[] allowedOptions)
	{
		var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		for (var index = startIndex; index < args.Length; index++)
		{
			var option = args[index];

			if (!allowedOptions.Contains(option, StringComparer.OrdinalIgnoreCase))
			{
				throw new ArgumentException($"Unknown option or unexpected argument: '{option}'.");
			}

			if (option.Equals("--discover", StringComparison.OrdinalIgnoreCase)
				|| option.Equals("--sync", StringComparison.OrdinalIgnoreCase))
			{
				if (!options.TryAdd(option, ""))
				{
					throw new ArgumentException($"Option '{option}' was specified more than once.");
				}
				continue;
			}

			if (index + 1 >= args.Length
				|| string.IsNullOrWhiteSpace(args[index + 1])
				|| args[index + 1].StartsWith('-'))
			{
				throw new ArgumentException($"Option '{option}' requires a value.");
			}

			var value = args[++index];
			if (headers is not null && option.Equals("--header", StringComparison.OrdinalIgnoreCase))
			{
				headers.Add(value);
				continue;
			}

			if (!options.TryAdd(option, value))
			{
				throw new ArgumentException($"Option '{option}' was specified more than once.");
			}
		}

		return options;
	}

	private static string GetServerName(string[] args)
	{
		if (args.Length < 3 || string.IsNullOrWhiteSpace(args[2]) || args[2].StartsWith('-'))
		{
			throw new ArgumentException("This command requires a server name.");
		}

		return args[2];
	}

	private static string GetConfigPath(Dictionary<string, string> options)
		=> options.GetValueOrDefault("--config", CliConfiguration.DefaultPath);

	private static string GetHostName(Dictionary<string, string> options)
		=> options.GetValueOrDefault("--host", CliConfiguration.DefaultHost);

	private static bool IsHelp(string argument)
		=> argument.Equals("--help", StringComparison.OrdinalIgnoreCase)
			|| argument.Equals("-h", StringComparison.OrdinalIgnoreCase);
}
