namespace BolaLens.Core.Model;

/// <summary>A named authorization policy and the requirement calls/types it adds.</summary>
public sealed record PolicyDefinition(string Name, IReadOnlyList<string> Requirements, SourceRef Location);

/// <summary>A class deriving AuthorizationHandler&lt;TRequirement, TResource&gt; (resource-based authorization).</summary>
public sealed record ResourceHandlerInfo(string HandlerType, string RequirementType, string ResourceType, SourceRef Location);

/// <summary>Global (application-wide) authentication/authorization configuration. It proves coarse
/// protection (authenticated user) but almost never object ownership — that is the project's point.</summary>
public sealed record PolicyMap(
    IReadOnlyList<string> AuthenticationSchemes,
    bool FallbackRequiresAuthenticatedUser,
    bool GlobalAuthorizeFilter,
    IReadOnlyList<PolicyDefinition> Policies,
    IReadOnlyList<ResourceHandlerInfo> ResourceHandlers,
    IReadOnlyList<UnresolvedItem> Unresolved)
{
    public static PolicyMap Empty { get; } = new([], false, false, [], [], []);
}
