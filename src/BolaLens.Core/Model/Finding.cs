namespace BolaLens.Core.Model;

/// <summary>One reported path with its symbolic, (optional) semantic and fused final verdict.</summary>
public sealed record Finding(
    string FindingId,
    ExecutionPath Path,
    SymbolicVerdict Symbolic,
    SemanticVerdict? Semantic,
    VerdictStatus Final,
    string FinalReason,
    string? Slice = null);
