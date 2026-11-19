namespace BolaLens.Eval;

/// <summary>Standard metrics; every ratio is null ("n/a") when its denominator is 0 — never a fake 0 %.
/// Operational recall counts Unknown-on-Vulnerable as a miss; FDR is reported separately from FPR.</summary>
public sealed record Metrics(double? Precision, double? Recall, double? OperationalRecall, double? F1, double? Fpr, double? Fdr, double? Coverage)
{
    public static Metrics From(ConfusionMatrix m)
    {
        static double? Ratio(int num, int den) => den == 0 ? null : (double)num / den;
        var decided = m.TP + m.FP + m.FN + m.TN;
        var eligible = decided + m.UnknownOnVulnerable + m.UnknownOnProtected;
        return new Metrics(
            Precision: Ratio(m.TP, m.TP + m.FP),
            Recall: Ratio(m.TP, m.TP + m.FN),
            OperationalRecall: Ratio(m.TP, m.TP + m.FN + m.UnknownOnVulnerable),
            F1: Ratio(2 * m.TP, 2 * m.TP + m.FP + m.FN),
            Fpr: Ratio(m.FP, m.FP + m.TN),
            Fdr: Ratio(m.FP, m.TP + m.FP),
            Coverage: Ratio(decided, eligible));
    }
}
