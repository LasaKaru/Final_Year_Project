using BolaLens.Core.Model;

namespace BolaLens.Eval;

/// <summary>Tool prediction for one endpoint unit: the worst path (Vulnerable &gt; Unknown &gt; Protected &gt; OutOfScope).</summary>
public sealed record UnitPrediction(string UnitKey, VerdictStatus Status, IReadOnlyList<string> RuleIds, string? FindingId);

public sealed record MatchedUnit(LabelEntry Label, UnitPrediction? Prediction);

/// <summary>Matches report findings to labels by normalised "VERB route" (unit = endpoint).
/// <paramref name="useSymbolic"/> selects the symbolic arm instead of the fused final verdict.</summary>
public static class UnitMatcher
{
    public static IReadOnlyDictionary<string, UnitPrediction> Predict(ScanReport report, bool useSymbolic = false)
    {
        return report.Findings
            .GroupBy(f => Routes.UnitKey(f.Path.Endpoint.Verb, f.Path.Endpoint.RouteTemplate))
            .ToDictionary(g => g.Key, g =>
            {
                var worst = g.MaxBy(f => VerdictOrdering.Rank(useSymbolic ? f.Symbolic.Status : f.Final))!;
                return new UnitPrediction(g.Key, useSymbolic ? worst.Symbolic.Status : worst.Final, worst.Symbolic.RuleIds, worst.FindingId);
            });
    }

    public static IReadOnlyList<MatchedUnit> Match(LabelSet labels, IReadOnlyDictionary<string, UnitPrediction> predictions) =>
        labels.Entries.Select(l => new MatchedUnit(l, predictions.TryGetValue(l.UnitKey, out var p) ? p : null)).ToList();
}
