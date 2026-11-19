using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace BolaLens.Analysis.Loading;

/// <summary>A solution opened through MSBuildWorkspace plus the load diagnostics that were reported.
/// Dispose it to release the workspace (and its build host process).</summary>
public sealed class LoadedSolution : IDisposable
{
    public required MSBuildWorkspace Workspace { get; init; }
    public required Solution Solution { get; init; }
    public required string RootDirectory { get; init; }
    public required string TargetPath { get; init; }
    public required IReadOnlyList<UnresolvedItem> Diagnostics { get; init; }

    public void Dispose() => Workspace.Dispose();
}
