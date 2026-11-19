using System.CommandLine;
using BolaLens.Analysis.Loading;
using BolaLens.Cli.Commands;

// FIRST: register the SDK's MSBuild before any MSBuild/MSBuildWorkspace type is loaded.
MsBuildBootstrap.EnsureRegistered();

var root = new RootCommand("BolaLens — context-aware BOLA detection for ASP.NET Core + EF Core (Roslyn + local SLM)");
root.Subcommands.Add(InventoryCommand.Create());
root.Subcommands.Add(ContextCommands.Endpoints());
root.Subcommands.Add(ContextCommands.Policy());
root.Subcommands.Add(ContextCommands.Schema());
root.Subcommands.Add(ContextCommands.Di());
root.Subcommands.Add(ContextCommands.Paths());
root.Subcommands.Add(SliceCommand.Create());
root.Subcommands.Add(ScanCommand.Create());
root.Subcommands.Add(EvalCommand.Create());
root.Subcommands.Add(ModelsCommand.Create());
root.Subcommands.Add(HtmlCommand.Create());
root.Subcommands.Add(SarifCommand.Create());
return await root.Parse(args).InvokeAsync();
