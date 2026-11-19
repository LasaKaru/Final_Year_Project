namespace BolaLens.Core.Model;

public enum SinkKind { Read, Update, Remove, Attach, SaveChanges, BulkUpdate, BulkDelete, Unknown }

/// <summary>An EF Core operation that changes (or reads) an entity selected by the tainted id.
/// <see cref="GuardIndices"/> points into <see cref="ExecutionPath.Guards"/>: the guards that were found
/// to validly protect *this* sink (same object, trusted identity, owner property, precedes the sink).</summary>
public sealed record SinkInfo(
    SinkKind Kind,
    string EntityType,
    bool IsStateChanging,
    bool BypassesQueryFilters,
    bool IsDetached,
    bool QueryHasOwnerPredicate,
    string Symbol,
    SourceRef Location,
    IReadOnlyList<int>? GuardIndices = null)
{
    public IReadOnlyList<int> ValidGuards => GuardIndices ?? [];
}
