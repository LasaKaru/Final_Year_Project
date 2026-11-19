using BolaLens.Core.Model;

namespace BolaLens.Analysis.Taint;

/// <summary>One concrete entity instance (or bulk query target) seen on a path. Reference identity of the
/// token *is* object identity: the same token flows through locals, arguments and return values across
/// controller → service → repository, so a guard and a sink can be proven to concern the same object.</summary>
public sealed class EntityToken
{
    private static int _next;

    public EntityToken(string entityType, string origin, SourceRef loadedAt)
    {
        EntityType = entityType;
        Origin = origin;
        LoadedAt = loadedAt;
        Number = Interlocked.Increment(ref _next);
    }

    public int Number { get; }
    public string EntityType { get; }
    public string ShortName => EntityType[(EntityType.LastIndexOf('.') + 1)..];
    public string Origin { get; }
    public SourceRef LoadedAt { get; }

    /// <summary>The object was selected by the tracked client-controlled id (key predicate / Find / detached key).</summary>
    public bool TaintSelected { get; init; }
    public bool Detached { get; init; }
    public bool BypassedFilters { get; init; }
    public bool IsCollection { get; init; }
    public OwnerKind? OwnerPredicateKind { get; init; }
    public string? OwnerPredicateProperty { get; init; }
    public string? OwnerPredicateCaller { get; init; }
    public SourceRef? OwnerPredicateLocation { get; init; }

    /// <summary>A property of the tracked entity was modified and not yet committed by SaveChanges.</summary>
    public bool Dirty { get; set; }
    public string? VariableName { get; set; }

    public override string ToString() => $"{ShortName}#{Number}{(VariableName is null ? "" : $"({VariableName})")}";
}
