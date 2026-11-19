using System.Diagnostics;
using BolaLens.Analysis.DI;
using BolaLens.Analysis.Endpoints;
using BolaLens.Analysis.Infrastructure;
using BolaLens.Analysis.Policy;
using BolaLens.Analysis.Schema;
using BolaLens.Analysis.Taint;
using BolaLens.Analysis.Verdict;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.Pipeline;

/// <summary>Everything Stage 1 produced for one solution.</summary>
public sealed record SymbolicResult(
    IReadOnlyList<EndpointInfo> Endpoints,
    PolicyMap Policy,
    OwnershipSchema Schema,
    IReadOnlyDictionary<string, IReadOnlyList<string>> DiMap,
    IReadOnlyList<(ExecutionPath Path, SymbolicVerdict Verdict)> Paths,
    int ProjectsLoaded,
    int ProjectsWithErrors,
    int UnsupportedEndpointStyles,
    IReadOnlyList<UnresolvedItem> Diagnostics,
    IReadOnlyDictionary<string, double> PhaseSeconds);

/// <summary>Orchestrates Stage 1: context → endpoints, policy, schema, DI → paths → symbolic verdicts.
/// Input: a Roslyn Solution (from MSBuildWorkspace or an AdhocWorkspace in tests).
/// Limitation: analyses every C# project; use the CLI on a single .csproj to narrow large solutions.</summary>
public sealed class SymbolicPipeline(BolaLensConfig config)
{
    public async Task<SymbolicResult> RunAsync(Solution solution, string rootDirectory, IReadOnlyList<UnresolvedItem>? loadDiagnostics = null,
        string? endpointFilter = null, CancellationToken ct = default)
    {
        var phases = new Dictionary<string, double>();
        var sw = Stopwatch.StartNew();
        var context = await AnalysisContext.CreateAsync(solution, rootDirectory, config, ct).ConfigureAwait(false);
        phases["compile"] = Lap(sw);

        var enumerator = new EndpointEnumerator(context);
        var endpoints = enumerator.Enumerate(ct);
        if (!string.IsNullOrWhiteSpace(endpointFilter))
        {
            var key = Routes.UnitKey(endpointFilter.Split(' ', 2)[0], endpointFilter.Split(' ', 2).ElementAtOrDefault(1) ?? "");
            endpoints = endpoints.Where(e => e.Info.Id == key || e.Info.Action == endpointFilter).ToList();
        }
        var policy = new GlobalPolicyMapper(context).Map(ct);
        var schema = new DbContextSchemaMapper(context).Map(ct);
        var di = DiRegistrationMap.Build(context, ct);
        var resolver = new ImplementationResolver(context, di);
        phases["context"] = Lap(sw);

        var paths = new PathExtractor(context, schema, resolver).Extract(endpoints, ct);
        phases["paths"] = Lap(sw);

        var classifier = new SymbolicClassifier(schema);
        var verdicts = paths.Select(p => (p, classifier.Classify(p))).ToList();
        phases["verdict"] = Lap(sw);

        var diagnostics = new List<UnresolvedItem>(loadDiagnostics ?? []);
        foreach (var pc in context.Compilations.Where(c => c.ErrorCount > 0))
            diagnostics.Add(new UnresolvedItem($"project {pc.Project.Name} has {pc.ErrorCount} compile errors (analysis may be unreliable — restore/build the target first)", null));
        diagnostics.AddRange(policy.Unresolved);
        diagnostics.AddRange(schema.Unresolved);

        return new SymbolicResult(endpoints.Select(e => e.Info).ToList(), policy, schema, di.ToDisplayMap(), verdicts,
            context.Compilations.Count, context.Compilations.Count(c => c.ErrorCount > 0), enumerator.UnsupportedEndpointStyles,
            diagnostics, phases);
    }

    private static double Lap(Stopwatch sw)
    {
        var s = sw.Elapsed.TotalSeconds;
        sw.Restart();
        return Math.Round(s, 3);
    }
}
