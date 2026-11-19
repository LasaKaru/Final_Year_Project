using BolaLens.Core.Model;

namespace BolaLens.Eval;

/// <summary>Binary confusion matrix (positive = Vulnerable) with the Unknown predictions kept separate:
/// U_vuln (label Vulnerable, predicted Unknown) and U_prot (label Protected, predicted Unknown).
/// Units labelled Unknown/OutOfScope are excluded from the matrix and counted as abstention cases.</summary>
public sealed record ConfusionMatrix(int TP, int FP, int FN, int TN, int UnknownOnVulnerable, int UnknownOnProtected,
    int Missing, int ExcludedLabels, int CorrectAbstentions)
{
    public static ConfusionMatrix From(IEnumerable<MatchedUnit> units)
    {
        int tp = 0, fp = 0, fn = 0, tn = 0, uv = 0, up = 0, missing = 0, excluded = 0, abst = 0;
        foreach (var u in units)
        {
            var predicted = u.Prediction?.Status ?? VerdictStatus.OutOfScope;
            if (u.Prediction is null) missing++;
            switch (u.Label.Label)
            {
                case VerdictStatus.Vulnerable:
                    if (predicted == VerdictStatus.Vulnerable) tp++;
                    else if (predicted == VerdictStatus.Unknown) uv++;
                    else fn++;
                    break;
                case VerdictStatus.Protected:
                    if (predicted == VerdictStatus.Vulnerable) fp++;
                    else if (predicted == VerdictStatus.Unknown) up++;
                    else tn++;
                    break;
                default:
                    excluded++;
                    if (predicted == VerdictStatus.Unknown) abst++;
                    break;
            }
        }
        return new ConfusionMatrix(tp, fp, fn, tn, uv, up, missing, excluded, abst);
    }
}
