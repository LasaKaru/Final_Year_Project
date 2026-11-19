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
    SourceRef? PredicateLocation);

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
        };
    }

    /// <summary>Data-only part (taint/client/identity), used when a value flows through an opaque operation.</summary>
    public AbstractValue DataOnly() =>
        Tainted || ClientControlled || Identity is not null
            ? new AbstractValue { Tainted = Tainted, ClientControlled = ClientControlled, Identity = Identity, IdentityExpression = IdentityExpression }
            : None;
}
