using System.CommandLine;
using System.Text.RegularExpressions;
using BolaLens.Analysis.Loading;
using Microsoft.CodeAnalysis;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens inventory &lt;path&gt;` — loads the target and lists projects, target frameworks,
/// document counts, compile-error counts and workspace diagnostics (Sprint 0 milestone).</summary>
public static partial class InventoryCommand
{
    [GeneratedRegex(@"<TargetFrameworks?>([^<]+)</TargetFrameworks?>")]
    private static partial Regex TfmRegex();

    public static Command Create()
    {
        var path = Common.PathArgument();
        var cmd = new Command("inventory", "Load a solution/project and list its projects, documents and diagnostics") { path };
        cmd.SetAction(async (ParseResult r, CancellationToken ct) =>
        {
            try
            {
                using var loaded = await new WorkspaceLoader().LoadAsync(r.GetValue(path)!, ct);
                Console.WriteLine($"Target: {loaded.TargetPath}");
                var totalErrors = 0;
                foreach (var project in loaded.Solution.Projects.OrderBy(p => p.Name))
                {
                    var compilation = await project.GetCompilationAsync(ct);
                    var errors = compilation?.GetDiagnostics(ct).Count(d => d.Severity == DiagnosticSeverity.Error) ?? -1;
                    totalErrors += Math.Max(errors, 0);
                    var tfm = project.FilePath is not null && File.Exists(project.FilePath)
                        ? TfmRegex().Match(await File.ReadAllTextAsync(project.FilePath, ct)).Groups[1].Value : "?";
                    Console.WriteLine($"  {project.Name}  [{project.Language}, {tfm}]  documents={project.Documents.Count()}  compileErrors={errors}");
                    Console.WriteLine($"    {project.FilePath}");
                    if (errors > 0 && compilation is not null)
                        foreach (var d in compilation.GetDiagnostics(ct).Where(d => d.Severity == DiagnosticSeverity.Error).Take(5))
                            Console.WriteLine($"    error {d.Id}: {d.GetMessage()}");
                }
                Console.WriteLine($"Projects: {loaded.Solution.Projects.Count()}  compile errors: {totalErrors}  workspace diagnostics: {loaded.Diagnostics.Count}");
                foreach (var d in loaded.Diagnostics) Console.WriteLine($"  [workspace] {d.Reason}");
                if (totalErrors > 0) Console.WriteLine("WARNING: compile errors usually mean unresolved references — run `dotnet restore` on the target; analysis may be unreliable.");
                return Common.Ok;
            }
            catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or InvalidOperationException or IOException)
            {
                Common.Error(ex.Message);
                return Common.LoadError;
            }
        });
        return cmd;
    }
}
