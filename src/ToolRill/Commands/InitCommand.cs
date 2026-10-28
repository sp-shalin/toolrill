using System.Text.Json;
using System.Text.Json.Nodes;
using ToolRill.Cli.Services;

namespace ToolRill.Cli.Commands;

internal static class InitCommand
{
	public static void Execute(string configPath)
	{
		var fullPath = Path.GetFullPath(configPath);

		if (File.Exists(fullPath))
		{
			throw new InvalidOperationException(
				$"Configuration file already exists: '{fullPath}'.");
		}

		var document = new JsonObject
		{
			["hosts"] = new JsonArray
			{
				new JsonObject
				{
					["name"] = CliConfiguration.DefaultHost,
					["incoming"] = new JsonObject { ["transport"] = "stdio" },
					["outgoing"] = new JsonArray()
				}
			}
		};
		var json = document.ToJsonString(
			new JsonSerializerOptions { WriteIndented = true });

		using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
		using (var writer = new StreamWriter(file))
		{
			writer.WriteLine(json);
		}

		Console.WriteLine($"Created configuration: {fullPath}");
		Console.WriteLine("Add a server with 'toolrill server add' before validating or starting.");
	}
}
