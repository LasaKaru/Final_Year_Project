using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace BolaLens.Analysis.Loading;

/// <summary>Opens a .sln/.slnx/.csproj with MSBuildWorkspace and records load failures.
/// Input: a path on disk. Limitation: the target must be restorable (run `dotnet restore` first);
/// projects that fail to evaluate are reported as diagnostics, not exceptions.</summary>
public sealed class WorkspaceLoader
{
    public async Task<LoadedSolution> LoadAsync(string path, CancellationToken ct)
    {
        MsBuildBootstrap.EnsureRegistered();

        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new FileNotFoundException($"Target not found: {full}", full);

        var ext = Path.GetExtension(full).ToLowerInvariant();
        if (ext is not (".csproj" or ".sln" or ".slnx"))
            throw new NotSupportedException($"Unsupported input '{ext}' (expected .sln, .slnx or .csproj)");

        var workspace = MSBuildWorkspace.Create();
        var diags = new List<UnresolvedItem>();
        workspace.WorkspaceFailed += (_, e) =>
        {
            lock (diags) diags.Add(new UnresolvedItem($"{e.Diagnostic.Kind}: {e.Diagnostic.Message}", null));
        };

        try
        {
            var solution = ext == ".csproj"
                ? (await workspace.OpenProjectAsync(full, cancellationToken: ct).ConfigureAwait(false)).Solution
                : await workspace.OpenSolutionAsync(full, cancellationToken: ct).ConfigureAwait(false);

            return new LoadedSolution
            {
                Workspace = workspace,
                Solution = solution,
                RootDirectory = Path.GetDirectoryName(full)!,
                TargetPath = full,
                Diagnostics = diags,
            };
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }
}
