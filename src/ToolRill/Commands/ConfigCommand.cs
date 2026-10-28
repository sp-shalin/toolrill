using ToolRill.Cli.Services;

namespace ToolRill.Cli.Commands;

internal static class ConfigCommand
{
	public static void Validate(string configPath, string? hostName = null)
	{
		var configuration = CliConfiguration.Load(configPath);
		using var configurationLifetime = configuration as IDisposable;
		var hosts = hostName is null
			? CliConfiguration.GetHosts(configuration)
			: new[] { CliConfiguration.GetHost(configuration, hostName) }.ToList();

		foreach (var host in hosts)
		{
			CliConfiguration.ValidateHost(host);
		}

		Console.WriteLine($"Configuration is valid: {Path.GetFullPath(configPath)}");
	}
}
