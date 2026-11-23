namespace BolaLens.Analysis.Verdict;

/// <summary>Rule catalogue (evaluated in this order; first match wins per sink). The IDs appear in the
/// report so every verdict is traceable to one documented rule.</summary>
public static class SymbolicRules
{
    public const string ReadOnly = "SYM-01";           // no state change, no tracked id lost in unresolved code → OutOfScope
    public const string UnresolvedFlow = "SYM-02";     // no state change found but the id entered unresolved code → Unknown
    public const string OwnerGuard = "SYM-03";         // valid owner equality / LINQ owner predicate → Protected
    public const string QueryFilter = "SYM-04";        // applicable query filter of the required owner level → Protected
    public const string ResourceAuthz = "SYM-05";      // resource-based AuthorizeAsync gates the change → Protected (delegated)
    public const string FilterBypass = "SYM-06";       // IgnoreQueryFilters and no valid guard → Vulnerable
    public const string DetachedOrBulk = "SYM-07";     // detached write / bulk write without owner predicate → Vulnerable
    public const string MissingGuard = "SYM-08";       // owned entity selected by id reaches a state change unguarded → Vulnerable
    public const string NoOwnershipModel = "SYM-09";   // entity has no owner candidate → Unknown
    public const string MembershipUnchecked = "SYM-11"; // membership-scoped object (library/organization) without an access check → Vulnerable
    public const string PrivilegedOnly = "SYM-10";     // endpoint restricted to configured privileged roles → OutOfScope (checked first)

    public static IReadOnlyDictionary<string, string> Descriptions { get; } = new Dictionary<string, string>
    {
        [ReadOnly] = "No state-changing EF Core sink is reached by the tracked id.",
        [UnresolvedFlow] = "No state change found, but the tracked id entered unresolved/ambiguous/too-deep code.",
        [OwnerGuard] = "A valid owner check (caller identity == owner property of the same object) precedes the change.",
        [QueryFilter] = "An EF Core global query filter at the required owner level applies to the object.",
        [ResourceAuthz] = "Resource-based authorization (IAuthorizationService.AuthorizeAsync) or a custom access filter attribute gates the change (delegated).",
        [FilterBypass] = "The object is loaded with IgnoreQueryFilters() and changed without a valid guard.",
        [DetachedOrBulk] = "Detached update by a client-controlled key, or bulk write without an owner predicate.",
        [MissingGuard] = "A client-controlled id selects an owned object that is changed without a valid guard or filter.",
        [NoOwnershipModel] = "The changed entity has no owner candidate (no ownership model to check against).",
        [MembershipUnchecked] = "An object scoped by a container users belong to (library, organization) is selected by a client id and changed or read with no access check.",
        [PrivilegedOnly] = "Only callers in configured privileged roles (analysis.privilegedRoles) reach the endpoint: horizontal BOLA needs peer users.",
    };
}
