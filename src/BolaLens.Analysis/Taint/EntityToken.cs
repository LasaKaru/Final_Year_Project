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
    public bool TaintSelected { get; set; }
    public bool Detached { get; init; }
    public bool BypassedFilters { get; init; }
    public bool IsCollection { get; init; }
    public OwnerKind? OwnerPredicateKind { get; init; }
    public string? OwnerPredicateProperty { get; init; }
    public string? OwnerPredicateCaller { get; init; }
    public SourceRef? OwnerPredicateLocation { get; init; }

    /// <summary>Provenance tags of the values used to select this object (key in Find / predicate).</summary>
    public HashSet<string> SelectorTags { get; } = new(StringComparer.Ordinal);

    /// <summary>Other tokens proven to be the same row (key equality with the value that loaded them).</summary>
    public HashSet<EntityToken> Aliases { get; } = [];

    /// <summary>Built from request data (body parameter / new T { Id = … }), not loaded from the database.</summary>
    public bool FromRequest { get; init; }

    public bool SameObject(EntityToken other) => ReferenceEquals(this, other) || Aliases.Contains(other);

    /// <summary>A property of the tracked entity was modified and not yet committed by SaveChanges.</summary>
    public bool Dirty { get; set; }
    public string? VariableName { get; set; }

    public override string ToString() => $"{ShortName}#{Number}{(VariableName is null ? "" : $"({VariableName})")}";
}
