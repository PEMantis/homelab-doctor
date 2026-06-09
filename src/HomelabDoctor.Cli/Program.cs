using System.CommandLine;
using HomelabDoctor.Cli.Commands;

var rootCommand = new RootCommand("Homelab Doctor — local-first diagnostics for self-hosted infrastructure");
rootCommand.Add(CheckCommand.Build());
rootCommand.Add(ReportCommand.Build());

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
