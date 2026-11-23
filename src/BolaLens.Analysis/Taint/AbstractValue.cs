using BolaLens.Core.Model;

namespace BolaLens.Analysis.Taint;

/// <summary>Owner property of a specific entity instance, e.g. order.UserId.</summary>
public sealed record OwnerValue(EntityToken Token, string Property, OwnerKind Kind);

/// <summary>An EF Core query under construction (DbSet → Where → Include → …) before it is executed.</summary>
public sealed record QueryValue(
    string EntityType,
    bool KeyTainted,
    bool BypassedFilters,
    OwnerKind? OwnerPredicateKind,
    string? OwnerPredicateProperty,
    string? OwnerPredicateCaller,
    SourceRef? PredicateLocation,
    IReadOnlySet<string>? SelectorTags = null,
    IReadOnlySet<string>? KeyTags = null,
    bool Projected = false);

/// <summary>An existence check such as AnyAsync(x =&gt; x.Id == id &amp;&amp; x.OwnerId == uid): "is this object the caller's?".</summary>
public sealed record ExistsCheck(string EntityType, bool KeyTainted, OwnerKind? OwnerKind, string? OwnerProperty, string? Caller);

/// <summary>The abstract value of an expression in the taint analysis (a small product lattice):
/// tracked id taint, generic client control, trusted caller identity, entity identity, query state,
/// owner-property provenance and resource-authorization results. Merge = union (may-analysis).</summary>
public sealed record AbstractValue
{
    public static AbstractValue None { get; } = new();

    /// <summary>Carries the tracked client-controlled id of this path.</summary>
    public bool Tainted { get; init; }

    /// <summary>Comes from the HTTP request (route/body/query/header), tracked id or not.</summary>
    public bool ClientControlled { get; init; }

    /// <summary>Server-side authenticated identity of the caller (User or Tenant level).</summary>
    public OwnerKind? Identity { get; init; }

    /// <summary>The caller's ClaimsPrincipal itself.</summary>
    public bool IsPrincipal { get; init; }

    public string? IdentityExpression { get; init; }
    public EntityToken? Entity { get; init; }
    public QueryValue? Query { get; init; }
    public OwnerValue? Owner { get; init; }

    /// <summary>An AuthorizationResult of AuthorizeAsync(User, resource, …) for this entity.</summary>
    public EntityToken? AuthResultFor { get; init; }
    public bool AuthForCaller { get; init; }

    /// <summary>Per-member values of an object created in the analysed code (e.g. new DeleteCommand(id)): lets taint
    /// follow exactly the members that received it. Null = unknown object (fall back to name heuristics).</summary>
    public IReadOnlyDictionary<string, AbstractValue>? Fields { get; init; }

    public ExistsCheck? Exists { get; init; }

    /// <summary>Provenance: which request inputs this value came from ("param:id", "param:dto.Id"). Used to prove that
    /// two objects are the same row (body.Id == id, and the stored object was loaded by id).</summary>
    public IReadOnlySet<string>? Tags { get; init; }

    /// <summary>This value is the key property of the given entity instance (e.g. notification.NotificationId).</summary>
    public EntityToken? KeyOf { get; init; }

    /// <summary>A bool flag set to true inside the guarded branch of these checks (update = true): a later
    /// if (update) { … } is protected by the same checks.</summary>
    public IReadOnlyList<GuardRecord>? GuardFlags { get; init; }

    public bool IsRelevant => Tainted || ClientControlled || Identity is not null || IsPrincipal || Entity is not null || Query is not null;

    public AbstractValue Merge(AbstractValue other)
    {
        if (ReferenceEquals(this, None)) return other;
        if (ReferenceEquals(other, None)) return this;
        return new AbstractValue
        {
            Tainted = Tainted || other.Tainted,
            ClientControlled = ClientControlled || other.ClientControlled,
            Identity = Identity ?? other.Identity,
            IsPrincipal = IsPrincipal || other.IsPrincipal,
            IdentityExpression = IdentityExpression ?? other.IdentityExpression,
            Entity = Entity ?? other.Entity,
            Query = Query ?? other.Query,
            Owner = Owner ?? other.Owner,
            AuthResultFor = AuthResultFor ?? other.AuthResultFor,
            AuthForCaller = AuthForCaller || other.AuthForCaller,
            Fields = MergeFields(Fields, other.Fields),
            Exists = Exists ?? other.Exists,
            Tags = Tags is null ? other.Tags : other.Tags is null ? Tags : Tags.Union(other.Tags).ToHashSet(),
            KeyOf = KeyOf ?? other.KeyOf,
            GuardFlags = GuardFlags is null ? other.GuardFlags : other.GuardFlags is null ? GuardFlags : [.. GuardFlags, .. other.GuardFlags],
        };
    }

    /// <summary>Data-only part (taint/client/identity/fields), used when a value flows through an opaque operation.</summary>
    public AbstractValue DataOnly() =>
        Tainted || ClientControlled || Identity is not null || Fields is not null || Tags is not null
            ? new AbstractValue { Tainted = Tainted, ClientControlled = ClientControlled, Identity = Identity, IdentityExpression = IdentityExpression, Fields = Fields, Tags = Tags }
            : None;

    private static IReadOnlyDictionary<string, AbstractValue>? MergeFields(IReadOnlyDictionary<string, AbstractValue>? a, IReadOnlyDictionary<string, AbstractValue>? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        var d = new Dictionary<string, AbstractValue>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in b) d[k] = d.TryGetValue(k, out var e) ? e.Merge(v) : v;
        return d;
    }
}
