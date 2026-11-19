using BolaLens.Core.Model;

namespace BolaLens.Inference;

/// <summary>Combines symbolic and semantic verdicts (threshold τ = suppressThreshold, tuned only on the
/// calibration split). The SLM can suppress a symbolic Vulnerable (the false-positive reduction under
/// test, RQ2). Only with <paramref name="allowEscalation"/> may it also escalate an Unknown or dispute a delegated
/// (SYM-05) Protected — off by default (ADR-012): on the development set both local models used those two rules only to
/// add errors. Input: both verdicts. Limitation: confidence is a model score, not a calibrated probability.</summary>
public sealed class FusionPolicy(double threshold = 0.80, bool allowEscalation = false)
{
    public double Threshold { get; } = threshold;
    public bool AllowEscalation { get; } = allowEscalation;

    /// <summary>Which symbolic outcomes are sent to the SLM (same candidate set in both arms, ADR-004).</summary>
    public static bool IsCandidate(SymbolicVerdict s) =>
        s.Status is VerdictStatus.Vulnerable or VerdictStatus.Unknown
        || (s.Status == VerdictStatus.Protected && s.RuleIds.Contains("SYM-05"));

    /// <param name="advisory">True when the model failed the calibration gate: its verdict is recorded, never applied.</param>
    public (VerdictStatus Final, string Reason) Fuse(SymbolicVerdict symbolic, SemanticVerdict? semantic, bool advisory = false)
    {
        if (semantic is null)
            return (symbolic.Status, IsCandidate(symbolic) ? "Symbolic only (semantic stage not run)." : "Symbolic verdict (not a semantic candidate).");
        if (advisory)
            return (symbolic.Status, $"Symbolic verdict kept: the model failed the calibration gate, so its answer ({semantic.Status}) is advisory only.");

        var confident = semantic.ParseOk && semantic.Confidence >= Threshold && semantic.EvidenceLineIds.Count > 0;
        switch (symbolic.Status)
        {
            case VerdictStatus.Vulnerable when semantic.Status == VerdictStatus.Vulnerable:
                return (VerdictStatus.Vulnerable, "Symbolic and semantic agree.");
            case VerdictStatus.Vulnerable when semantic.Status == VerdictStatus.Protected && confident:
                return (VerdictStatus.Protected, $"Suppressed by SLM (confidence {semantic.Confidence:0.00} ≥ τ {Threshold:0.00}): {semantic.Reason}");
            case VerdictStatus.Vulnerable:
                return (VerdictStatus.Vulnerable, semantic.ParseOk ? "SLM not confident enough to suppress." : "SLM output invalid; symbolic verdict kept.");
            case VerdictStatus.Unknown when AllowEscalation && semantic.Status == VerdictStatus.Vulnerable && confident:
                return (VerdictStatus.Vulnerable, $"Escalated by SLM (confidence {semantic.Confidence:0.00}): {semantic.Reason}");
            case VerdictStatus.Unknown:
                return (VerdictStatus.Unknown, AllowEscalation ? "Symbolic Unknown; SLM did not confidently resolve it." : $"Symbolic Unknown kept (escalation disabled); SLM said {semantic.Status}.");
            case VerdictStatus.Protected when AllowEscalation && symbolic.RuleIds.Contains("SYM-05") && semantic.Status == VerdictStatus.Vulnerable && confident:
                return (VerdictStatus.Vulnerable, $"SLM disputes the delegated authorization check: {semantic.Reason}");
            default:
                return (symbolic.Status, "Symbolic verdict kept.");
        }
    }
}
