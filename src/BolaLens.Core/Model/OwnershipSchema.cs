namespace BolaLens.Core.Model;

public enum OwnerKind { User, Tenant, Organisation, Unknown }

public enum OwnerEvidence { NamePattern, ForeignKeyToUserType, QueryFilter, Configured }

/// <summary>A property that probably records who owns an entity instance. A *candidate* only: the schema
/// cannot prove the intended policy.</summary>
public sealed record OwnerCandidate(string Property, OwnerKind Kind, IReadOnlyList<OwnerEvidence> Evidence);

/// <summary>An EF Core global query filter (HasQueryFilter) and the entity properties it references.</summary>
public sealed record QueryFilterInfo(string Expression, IReadOnlyList<string> ReferencedProperties, OwnerKind ScopeKind, SourceRef Location);

/// <summary>An EF Core entity as seen through a DbContext: keys, owner candidates and query filters.</summary>
public sealed record EntityModel(
    string EntityType,
    string DbContext,
    string? DbSetName,
    IReadOnlyList<string> KeyProperties,
    IReadOnlyList<OwnerCandidate> OwnerCandidates,
    IReadOnlyList<QueryFilterInfo> QueryFilters)
{
    /// <summary>Short (unqualified) type name, e.g. "Order".</summary>
    public string ShortName => EntityType[(EntityType.LastIndexOf('.') + 1)..];

    /// <summary>The owner level a valid guard must check: User if any user owner exists, else Tenant, else none.</summary>
    public OwnerKind? RequiredOwnerKind =>
        OwnerCandidates.Any(o => o.Kind == OwnerKind.User) ? OwnerKind.User
        : OwnerCandidates.Any(o => o.Kind is OwnerKind.Tenant or OwnerKind.Organisation) ? OwnerKind.Tenant
        : null;
}

/// <summary>All entities of all DbContexts in the analysed solution.</summary>
public sealed record OwnershipSchema(IReadOnlyList<EntityModel> Entities, IReadOnlyList<UnresolvedItem> Unresolved)
{
    public static OwnershipSchema Empty { get; } = new([], []);

    public EntityModel? Find(string entityType) =>
        Entities.FirstOrDefault(e => e.EntityType == entityType)
        ?? Entities.FirstOrDefault(e => e.ShortName == entityType);
}
