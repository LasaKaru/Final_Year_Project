using BolaLens.Core.Model;

namespace BolaLens.Eval;

/// <summary>One evaluated arm (symbolic-only or hybrid) of one report.</summary>
public sealed record EvalArm(string Name, ConfusionMatrix Matrix, Metrics Metrics, IReadOnlyList<MatchedUnit> Units, bool IsBaseline = false);

/// <summary>Evaluation of one or more reports against one label set.</summary>
public sealed record EvalResult(string Title, IReadOnlyList<EvalArm> Arms)
{
    public string ToMarkdown() => MarkdownTable.Render(Title, Arms.Select(a => (a.Name, a.Matrix, a.Metrics)).ToList(), Arms.Last(a => !a.IsBaseline).Units);
}

/// <summary>Builds evaluation arms: every report gives a symbolic arm; a hybrid report also gives a
/// hybrid arm (same candidates → clean ablation). Extra reports (e.g. another model) add more arms.</summary>
public static class Evaluator
{
    public static EvalResult Evaluate(LabelSet labels, IReadOnlyList<(string Label, ScanReport Report)> reports, string? title = null)
    {
        var arms = new List<EvalArm>();
        var symbolicSeen = false;
        foreach (var (label, report) in reports)
        {
            if (!symbolicSeen || report.Run.Mode == "symbolic")
            {
                arms.Add(Arm(reports.Count > 1 ? $"symbolic ({label})" : "symbolic", labels, report, useSymbolic: true));
                symbolicSeen = true;
            }
            if (report.Run.Mode == "hybrid")
            {
                var gate = report.Run.ModelGate is not { } g ? "no gate"
                    : g.Passed ? $"gate passed {g.Correct}/{g.Total}" : $"gate failed {g.Correct}/{g.Total} → advisory";
                arms.Add(Arm($"hybrid ({report.Run.ModelId ?? label}, {gate})", labels, report, useSymbolic: false));
            }
        }
        var first = reports[0].Report.Run;
        return new EvalResult(title ?? $"Evaluation — {first.TargetPath} ({first.StartedUtc:yyyy-MM-dd})", arms);
    }

    private static EvalArm Arm(string name, LabelSet labels, ScanReport report, bool useSymbolic)
    {
        var units = UnitMatcher.Match(labels, UnitMatcher.Predict(report, useSymbolic));
        var m = ConfusionMatrix.From(units);
        return new EvalArm(name, m, Metrics.From(m), units);
    }
}
