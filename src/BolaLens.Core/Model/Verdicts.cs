namespace BolaLens.Core.Model;

/// <summary>Three-state verdict (ADR-003) plus OutOfScope for paths with no state change.</summary>
public enum VerdictStatus { Vulnerable, Protected, Unknown, OutOfScope }

/// <summary>Stage 1 verdict with the IDs of the rules that fired (explainability).</summary>
public sealed record SymbolicVerdict(VerdictStatus Status, IReadOnlyList<string> RuleIds, string Rationale);

/// <summary>Stage 2 verdict from the local SLM. Confidence is a model score, NOT a calibrated probability.</summary>
public sealed record SemanticVerdict(
    VerdictStatus Status,
    double? Confidence,
    string Reason,
    IReadOnlyList<string> EvidenceLineIds,
    bool ParseOk,
    string ModelId,
    long LatencyMs,
    string? RawOutput,
    int Attempts = 1);

public static class VerdictOrdering
{
    /// <summary>Severity rank used to aggregate several paths of one endpoint: Vulnerable &gt; Unknown &gt; Protected &gt; OutOfScope.</summary>
    public static int Rank(VerdictStatus s) => s switch
    {
        VerdictStatus.Vulnerable => 3,
        VerdictStatus.Unknown => 2,
        VerdictStatus.Protected => 1,
        _ => 0
    };

    public static VerdictStatus Worst(IEnumerable<VerdictStatus> statuses) =>
        statuses.DefaultIfEmpty(VerdictStatus.OutOfScope).MaxBy(Rank);
}
