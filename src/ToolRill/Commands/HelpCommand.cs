namespace ToolRill.Cli.Commands;

internal static class HelpCommand
{
    public static void Execute(params string[] topics)
    {
        var topic = string.Join(" ", topics).ToLowerInvariant();
        var usage = topic switch
        {
            "" => """
                ToolRill - Native .NET MCP Host

                Usage: toolrill <command> [options]

                Commands:
                  init             Create a configuration file without overwriting.
                  server add       Add a downstream server to the base configuration.
                  server list      List effective configured servers without connecting.
                  server inspect   Discover live downstream tools; optionally sync filters.
                  server remove    Remove a server from the base configuration.
                  config validate  Validate effective configuration without connecting.
                  start            Start the selected host using its incoming settings.
                  manager          Start all configured HTTP hosts in the foreground.
                  help [command]   Show usage, also available with --help or -h.

                Existing modes: stdio, serve-http, manager.
                No command starts the default host using its incoming settings.
                Configuration defaults to ./toolrill.json in the working directory.
                Hosts contain incoming settings and outgoing downstream servers.
                """,

            "init" => """
                Usage: toolrill init [--config FILE]

                Creates toolrill.json with a default stdio host and empty outgoing array.
                An explicit --config selects another file. Existing files are not overwritten.
                Add a server before validating or starting.
                """,

            "server" or "server add" or "server list" or "server remove" or "server inspect" => """
                Usage:
                  toolrill server add <name> --transport stdio --project PATH [--command COMMAND] [--discover] [--host NAME] [--config FILE]
                  toolrill server add <name> --transport stdio --command COMMAND [--args JSON_ARRAY] [--discover] [--host NAME] [--config FILE]
                  toolrill server add <name> --transport streamable-http --url URL [--header KEY=VALUE]... [--discover] [--host NAME] [--config FILE]
                  toolrill server list [--host NAME] [--config FILE]
                  toolrill server remove <name> [--host NAME] [--config FILE]
                                    toolrill server inspect <name> [--host NAME] [--config FILE] [--sync]

                                Add accepts --discover to save live raw tool names in tools.include.
                                Inspect lists live names/descriptions; --sync replaces base tools.include.
                                Discovery finishes and disconnects before saving; failure leaves the file unchanged.
                                Inspect uses effective settings; disabled servers are rejected. Excludes are preserved.
                --host selects a host configuration and defaults to default.
                <name> is your downstream server name, such as inventory, not a keyword.
                Tools are exposed as <server-name>/<tool-name>, such as inventory/search.
                With --project, command defaults to dotnet: <command> run --project <path>.
                Otherwise --command names an executable; --args is a JSON string array.
                Nonempty args cannot be combined with projectPath. Set env in the JSON file.
                HTTP requires an absolute URL. New servers are enabled by default.
                --header is repeatable for HTTP; URL/header values expand ${ENV_VAR}.
                Add/remove edit only the selected host's outgoing array in the base file.
                Host names and server names are case-insensitive.
                List reads effective configuration, including environment overrides.
                """,

            "config" or "config validate" => """
                Usage: toolrill config validate [--host NAME] [--config FILE]

                Validates all hosts, or only --host NAME, without connecting.
                Reads environment overrides. Each validated host needs outgoing servers.
                """,

            "start" => """
                Usage: toolrill start [--host NAME] [--mode stdio|serve-http] [--config FILE] [--url URL]

                Runs in the foreground; press Ctrl+C to stop.
                --host defaults to default; incoming settings supply transport and URL.
                --mode and --url override settings for this run without editing the file.
                --url applies only to HTTP. Clients connect to the incoming URL plus /mcp.
                Switching a stdio host to HTTP defaults to http://127.0.0.1:3001.
                """,

            "stdio" => """
                Usage: toolrill stdio [--host NAME] [--config FILE]

                Starts the selected host using stdio, overriding its incoming transport.
                --host defaults to default. MCP clients launch stdio hosts individually.
                """,

            "serve-http" => """
                Usage: toolrill serve-http [--host NAME] [--config FILE] [--url URL]

                Equivalent to toolrill start --mode serve-http.
                --host defaults to default; reads its incoming URL unless --url is supplied.
                Switching a stdio host to HTTP defaults to http://127.0.0.1:3001.
                """,

            "manager" => """
                Usage: toolrill manager [--config FILE]

                Starts all streamable-http hosts from toolrill.json; skips stdio hosts.
                Each process uses its own incoming URL and outgoing servers from that file.
                HTTP listening addresses must be distinct. Press Ctrl+C to stop all hosts.
                """,

            "help" => """
                Usage: toolrill help [command] [subcommand]

                Examples: toolrill help start, toolrill help server add.
                Commands also accept --help or -h without loading configuration.
                """,

            _ => throw new ArgumentException($"Unknown help topic '{topic}'.")
        };

        Console.WriteLine(usage);
    }
}