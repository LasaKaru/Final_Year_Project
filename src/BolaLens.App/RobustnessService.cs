using System.Diagnostics;
using System.Text;
using BolaLens.Analysis.Loading;
using BolaLens.Analysis.Pipeline;
using BolaLens.Analysis.Robustness;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using BolaLens.Slicing;
using Microsoft.CodeAnalysis;

namespace BolaLens.App;

/// <summary>Verdict of one endpoint unit before and after a perturbation.</summary>
public sealed record UnitChange(string Unit, VerdictStatus Before, VerdictStatus After);

/// <summary>Effect of one perturbation on the symbolic verdicts (and, for slices, on the text the SLM would see).</summary>
public sealed record RobustnessRow(
    string PerturbationId, string Description, bool PreservesSemantics,
    int Units, int Unchanged, IReadOnlyList<UnitChange> Changes,
    int CompileErrorsBefore, int CompileErrorsAfter,
    int SlicesCompared, int SlicesIdentical, double Seconds)
{
    public double Stability => Units == 0 ? 1.0 : (double)Unchanged / Units;
}

public sealed record RobustnessResult(string Target, IReadOnlyList<RobustnessRow> Rows)
{
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Robustness — {Target}").AppendLine();
        sb.AppendLine("Units = endpoints with at least one traced path; a unit is unchanged when its worst symbolic verdict is the same.")
          .AppendLine("Semantics-preserving perturbations should give stability 1.000; P4 measures dependence on owner-name heuristics.").AppendLine();
        sb.AppendLine("| Perturbation | Preserves semantics | Units | Unchanged | Stability | Compile errors before → after | Identical slices (line numbers ignored) | Time |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var r in Rows)
            sb.AppendLine($"| {r.PerturbationId} — {r.Description} | {(r.PreservesSemantics ? "yes" : "no")} | {r.Units} | {r.Unchanged} | {r.Stability:0.000} " +
                          $"| {r.CompileErrorsBefore} → {r.CompileErrorsAfter} | {(r.SlicesCompared == 0 ? "n/a" : $"{r.SlicesIdentical}/{r.SlicesCompared}")} | {r.Seconds:0.0} s |");
        foreach (var r in Rows.Where(r => r.Changes.Count > 0))
        {
            sb.AppendLine().AppendLine($"## Changed units — {r.PerturbationId}").AppendLine().AppendLine("| Unit | Before | After |").AppendLine("|---|---|---|");
            foreach (var c in r.Changes) sb.AppendLine($"| `{c.Unit}` | {c.Before} | {c.After} |");
        }
        return sb.ToString();
    }
}

/// <summary>Robustness runner (FR21): loads the target once, analyses it, then re-analyses in-memory perturbed copies
/// (nothing is written to disk, nothing is executed) and compares the per-endpoint verdicts and the IR slices.</summary>
public sealed partial class RobustnessService(BolaLensConfig config)
{
    [System.Text.RegularExpressions.GeneratedRegex(@"\.cs:\d+")]
    private static partial System.Text.RegularExpressions.Regex LineRefRegex();
    private static readonly System.Text.RegularExpressions.Regex LineRef = LineRefRegex();

    public async Task<RobustnessResult> RunAsync(string target, IReadOnlyList<Perturbation> perturbations, CancellationToken ct,
        IProgress<string>? progress = null)
    {
        progress?.Report("Loading solution (MSBuildWorkspace)…");
        using var loaded = await new WorkspaceLoader().LoadAsync(target, ct).ConfigureAwait(false);
        var pipeline = new SymbolicPipeline(config);
        progress?.Report("Baseline analysis…");
        var baseline = await pipeline.RunAsync(loaded.Solution, loaded.RootDirectory, loaded.Diagnostics, ct: ct).ConfigureAwait(false);
        var before = Units(baseline);
        var slicesBefore = Slices(baseline);
        var errorsBefore = await CompileErrorsAsync(loaded.Solution, ct).ConfigureAwait(false);

        var rows = new List<RobustnessRow>();
        foreach (var p in perturbations)
        {
            progress?.Report($"{p.Id}: applying and re-analysing…");
            var sw = Stopwatch.StartNew();
            var perturbed = await p.Apply(loaded.Solution, config, ct).ConfigureAwait(false);
            var result = await pipeline.RunAsync(perturbed, loaded.RootDirectory, loaded.Diagnostics, ct: ct).ConfigureAwait(false);
            var after = Units(result);
            var changes = before.Keys.Union(after.Keys).Order(StringComparer.Ordinal)
                .Select(u => new UnitChange(u, before.GetValueOrDefault(u, VerdictStatus.OutOfScope), after.GetValueOrDefault(u, VerdictStatus.OutOfScope)))
                .Where(c => c.Before != c.After).ToList();
            var units = before.Keys.Union(after.Keys).Count();
            var slicesAfter = Slices(result);
            var compared = slicesBefore.Keys.Intersect(slicesAfter.Keys).ToList();
            var identical = compared.Count(k => slicesBefore[k] == slicesAfter[k]);
            var errorsAfter = await CompileErrorsAsync(perturbed, ct).ConfigureAwait(false);
            rows.Add(new RobustnessRow(p.Id, p.Description, p.PreservesSemantics, units, units - changes.Count, changes,
                errorsBefore, errorsAfter, compared.Count, identical, sw.Elapsed.TotalSeconds));
        }
        return new RobustnessResult(target, rows);
    }

    private static Dictionary<string, VerdictStatus> Units(SymbolicResult r) =>
        r.Paths.GroupBy(p => Routes.UnitKey(p.Path.Endpoint.Verb, p.Path.Endpoint.RouteTemplate))
            .ToDictionary(g => g.Key, g => VerdictOrdering.Worst(g.Select(p => p.Verdict.Status)), StringComparer.Ordinal);

    private Dictionary<string, string> Slices(SymbolicResult r)
    {
        var slicer = new IrSliceGenerator(r.Policy, r.Schema, config.Inference.MaxSliceTokens);
        // line references (File.cs:42) move when lines are inserted; compare the content the model would read
        return r.Paths.GroupBy(p => p.Path.Id).ToDictionary(g => g.Key,
            g => LineRef.Replace(slicer.Generate(g.First().Path).Text, ".cs:#"), StringComparer.Ordinal);
    }

    private static async Task<int> CompileErrorsAsync(Solution solution, CancellationToken ct)
    {
        var total = 0;
        foreach (var project in solution.Projects.Where(p => p.Language == LanguageNames.CSharp))
            if (await project.GetCompilationAsync(ct).ConfigureAwait(false) is { } c)
                total += c.GetDiagnostics(ct).Count(d => d.Severity == DiagnosticSeverity.Error);
        return total;
    }
}
