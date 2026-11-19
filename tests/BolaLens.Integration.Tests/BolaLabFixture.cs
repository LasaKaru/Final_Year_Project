using System.Runtime.CompilerServices;
using BolaLens.Analysis.Loading;
using BolaLens.Analysis.Pipeline;
using BolaLens.Cli.Scanning;
using BolaLens.Core.Config;
using BolaLens.Core.Model;

[assembly: AssemblyFixture(typeof(BolaLens.Integration.Tests.BolaLabFixture))]

namespace BolaLens.Integration.Tests;

internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Init() => MsBuildBootstrap.EnsureRegistered();
}

/// <summary>Loads fixtures/BolaLab once through the real MSBuildWorkspace and runs a symbolic scan.
/// The fixture must be restored/built first (`dotnet build fixtures/BolaLab/BolaLab.sln`), as in CI.</summary>
public sealed class BolaLabFixture : IAsyncLifetime
{
    public static string RepoRoot { get; } = FindRoot();
    public static string SolutionPath => Path.Combine(RepoRoot, "fixtures", "BolaLab", "BolaLab.sln");
    public static string ExpectedPath => Path.Combine(RepoRoot, "fixtures", "BolaLab", "expected.json");

    public ScanReport Report { get; private set; } = null!;
    public SymbolicResult Symbolic { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var assets = Path.Combine(RepoRoot, "fixtures", "BolaLab", "src", "BolaLab.Api", "obj", "project.assets.json");
        if (!File.Exists(assets))
            throw new InvalidOperationException("BolaLab is not restored. Run: dotnet build fixtures/BolaLab/BolaLab.sln");
        (Report, Symbolic) = await new ScanService(BolaLensConfig.Default).ScanAsync(SolutionPath, ScanMode.Symbolic, CancellationToken.None);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "BolaLens.sln"))) return d.FullName;
        throw new InvalidOperationException("Repository root (BolaLens.sln) not found");
    }
}
