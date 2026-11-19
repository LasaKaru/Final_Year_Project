namespace BolaLens.Core.Model;

/// <summary>One tagged line of an IR slice, e.g. "L5 [LOAD] var order = …".</summary>
public sealed record IrLine(string LineId, string Tag, string Text, SourceRef? Source);

/// <summary>A compact, label-free textual view of one execution path for the local SLM (Stage 2).
/// It never contains the symbolic verdict or rule IDs (keeps the ablation fair).</summary>
public sealed record IrSlice(string PathId, IReadOnlyList<IrLine> Lines, int EstimatedTokens, int TruncatedLines)
{
    public string Text => string.Join('\n', Lines.Select(l => $"{l.LineId,-4}[{l.Tag}] {l.Text}"));

    public bool HasLine(string lineId) => Lines.Any(l => l.LineId == lineId);

    /// <summary>Source map L# → SourceRef (for grounding the SLM's evidence and anchoring the report).</summary>
    public IReadOnlyDictionary<string, SourceRef?> SourceMap => Lines.ToDictionary(l => l.LineId, l => l.Source);
}
