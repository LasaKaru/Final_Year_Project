namespace BolaLens.Core.Model;

/// <summary>A location in analysed source code (repo-relative path, 1-based lines) plus an optional
/// whitespace-normalised, comment-free snippet of the statement. Roslyn locations are converted to
/// this record inside BolaLens.Analysis so that no Roslyn type ever leaves the analysis layer.</summary>
public sealed record SourceRef(string FilePath, int StartLine, int EndLine, string? Snippet = null)
{
    public override string ToString() => $"{FilePath}:{StartLine}";
}

/// <summary>Something the tool could not resolve or interpret. Recorded instead of silently ignored
/// (NFR07 failure transparency): an unresolved item can turn a verdict into Unknown.</summary>
public sealed record UnresolvedItem(string Reason, SourceRef? Location);
