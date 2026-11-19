using Microsoft.CodeAnalysis.CSharp;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace BolaLens.Analysis.Loading;

/// <summary>Opens a .sln/.slnx/.csproj with MSBuildWorkspace and records load failures.
/// Input: a path on disk. Limitation: the target must be restorable (run `dotnet restore` first);
/// projects that fail to evaluate are reported as diagnostics, not exceptions.</summary>
public sealed class WorkspaceLoader
{
    /// <summary>C# project paths listed in a .slnx file (folders are nested elements; project paths are relative).</summary>
    /// <summary>Projects targeting .NET 10+ may use C# 14 (extension blocks, the field keyword). The bundled Roslyn (4.14,
    /// the last version whose MSBuild host runs on .NET 8) knows these only as preview features, so such projects are parsed
    /// with LanguageVersion.Preview; otherwise whole files fail to parse and every call into them is unresolved.</summary>
    public static Solution UpgradeLanguageVersion(Solution solution)
    {
        foreach (var project in solution.Projects)
        {
            if (project.ParseOptions is not CSharpParseOptions po) continue;
            if (!po.PreprocessorSymbolNames.Contains("NET10_0_OR_GREATER")) continue;
            if (po.LanguageVersion == LanguageVersion.Preview) continue;
            solution = solution.WithProjectParseOptions(project.Id, po.WithLanguageVersion(LanguageVersion.Preview));
        }
        return solution;
    }

    public static IReadOnlyList<string> SlnxProjects(string slnxPath)
    {
        var dir = Path.GetDirectoryName(slnxPath)!;
        var doc = System.Xml.Linq.XDocument.Load(slnxPath);
        return doc.Descendants().Where(e => e.Name.LocalName == "Project")
            .Select(e => (string?)e.Attribute("Path"))
            .Where(p => p is not null && p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFullPath(Path.Combine(dir, p!.Replace('\\', Path.DirectorySeparatorChar))))
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<LoadedSolution> LoadAsync(string path, CancellationToken ct)
    {
        MsBuildBootstrap.EnsureRegistered();

        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException($"Target not found: {full}", full);

        var ext = Path.GetExtension(full).ToLowerInvariant();
        if (ext is not (".csproj" or ".sln" or ".slnx"))
            throw new NotSupportedException($"Unsupported input '{ext}' (expected .sln, .slnx or .csproj)");

        // Only design-time evaluation is needed: NuGet audit warnings and warnings-as-errors in the target must not
        // make projects fail to load (real repos often pin packages with advisories).
        var workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
        {
            ["NuGetAudit"] = "false",
            ["TreatWarningsAsErrors"] = "false",
            ["WarningsAsErrors"] = "",
            ["RunAnalyzers"] = "false",
        });
        var diags = new List<UnresolvedItem>();
        workspace.WorkspaceFailed += (_, e) =>
        {
            lock (diags) diags.Add(new UnresolvedItem($"{e.Diagnostic.Kind}: {e.Diagnostic.Message}", null));
        };

        try
        {
            Solution solution;
            if (ext == ".csproj")
            {
                solution = (await workspace.OpenProjectAsync(full, cancellationToken: ct).ConfigureAwait(false)).Solution;
            }
            else if (ext == ".slnx")
            {
                // .slnx (XML solution format) is read here and each project opened, so loading does not depend on the
                // Roslyn/MSBuild version's solution parser.
                foreach (var project in SlnxProjects(full))
                {
                    if (workspace.CurrentSolution.Projects.Any(p => string.Equals(p.FilePath, project, StringComparison.OrdinalIgnoreCase))) continue;
                    try
                    {
                        await workspace.OpenProjectAsync(project, cancellationToken: ct).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        lock (diags) diags.Add(new UnresolvedItem($"Failure: could not load {Path.GetFileName(project)}: {ex.Message}", null));
                    }
                }
                solution = workspace.CurrentSolution;
            }
            else
            {
                solution = await workspace.OpenSolutionAsync(full, cancellationToken: ct).ConfigureAwait(false);
            }

            solution = UpgradeLanguageVersion(solution);
            return new LoadedSolution
            {
                Workspace = workspace,
                Solution = solution,
                RootDirectory = Path.GetDirectoryName(full)!,
                TargetPath = full,
                Diagnostics = diags,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not FileNotFoundException and not NotSupportedException)
        {
            workspace.Dispose();
            // MSBuild/evaluation failures (e.g. unsupported SDK) become a friendly load error (exit code 2), not a crash.
            throw new InvalidOperationException($"Could not load '{Path.GetFileName(full)}': {ex.GetType().Name}: {ex.Message}", ex);
        }
    }
}
