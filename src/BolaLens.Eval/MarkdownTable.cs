using System.Globalization;
using System.Text;

namespace BolaLens.Eval;

/// <summary>Renders evaluation results as Markdown (for eval/runs/*/metrics.md and the IPD slides).</summary>
public static class MarkdownTable
{
    public static string Fmt(double? v) => v is null ? "n/a" : v.Value.ToString("0.000", CultureInfo.InvariantCulture);

    public static string Render(string title, IReadOnlyList<(string Arm, ConfusionMatrix Matrix, Metrics Metrics)> arms, IReadOnlyList<MatchedUnit> units)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {title}").AppendLine();
        sb.AppendLine("| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var (arm, m, x) in arms)
            sb.AppendLine($"| {arm} | {m.TP} | {m.FP} | {m.FN} | {m.TN} | {m.UnknownOnVulnerable} | {m.UnknownOnProtected} | {Fmt(x.Precision)} | {Fmt(x.Recall)} | {Fmt(x.OperationalRecall)} | {Fmt(x.F1)} | {Fmt(x.Fpr)} | {Fmt(x.Fdr)} | {Fmt(x.Coverage)} |");
        sb.AppendLine();
        var first = arms.FirstOrDefault().Matrix;
        if (first is not null)
            sb.AppendLine($"Units labelled Unknown/OutOfScope (excluded from the matrix): {first.ExcludedLabels}; correctly abstained (predicted Unknown): {first.CorrectAbstentions}; labelled units with no prediction: {first.Missing}.").AppendLine();

        sb.AppendLine("| Unit | TC | Label | Expected rule | Predicted | Rules | Match |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var u in units)
        {
            var p = u.Prediction;
            var ok = p is not null && p.Status == u.Label.Label;
            var rule = p is not null && u.Label.ExpectedRule is not null && p.RuleIds.Contains(u.Label.ExpectedRule);
            sb.AppendLine($"| `{u.Label.UnitKey}` | {u.Label.Tc} | {u.Label.Label} | {u.Label.ExpectedRule} | {p?.Status.ToString() ?? "—"} | {(p is null ? "" : string.Join(",", p.RuleIds))} | {(ok ? (rule || u.Label.ExpectedRule is null ? "✅" : "✅ (rule differs)") : "❌")} |");
        }
        return sb.ToString();
    }
}
