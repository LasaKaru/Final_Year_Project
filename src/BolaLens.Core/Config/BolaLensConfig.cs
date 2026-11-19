using System.Security.Cryptography;
using System.Text;
using BolaLens.Core.Json;

namespace BolaLens.Core.Config;

/// <summary>Root of bolalens.json. Every heuristic the analyser uses is listed here so that the
/// assumptions are explicit and defensible. Compiled-in defaults; a file overrides per property.</summary>
public sealed class BolaLensConfig
{
    public AnalysisOptions Analysis { get; init; } = new();
    public IdentityOptions Identity { get; init; } = new();
    public OwnershipOptions Ownership { get; init; } = new();
    public InferenceOptions Inference { get; init; } = new();

    public static BolaLensConfig Default { get; } = new();

    /// <summary>Loads a config file (or the defaults if <paramref name="path"/> is null).</summary>
    public static BolaLensConfig Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return new BolaLensConfig();
        if (!File.Exists(path)) throw new FileNotFoundException("Config file not found", path);
        return JsonDefaults.Deserialize<BolaLensConfig>(File.ReadAllText(path));
    }

    /// <summary>SHA-256 (first 16 hex) of the effective configuration, written into every report.</summary>
    public string Hash()
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonDefaults.Serialize(this)));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

public sealed class AnalysisOptions
{
    public int MaxCallDepth { get; init; } = 5;
    public bool IncludeReadOnlyPaths { get; init; }
    public IReadOnlyList<string> IdParameterPatterns { get; init; } = ["^id$", "Id$", "Key$"];
    public IReadOnlyList<string> ParseLikeMethods { get; init; } = ["Parse", "TryParse", "ToInt32", "ToInt64", "ToString", "ToGuid"];
}

public sealed class IdentityOptions
{
    public IReadOnlyList<string> ClaimsPrincipalMembers { get; init; } = ["User", "HttpContext.User"];
    public IReadOnlyList<string> ClaimAccessMethods { get; init; } = ["FindFirst", "FindFirstValue", "HasClaim"];
    public IReadOnlyList<CurrentUserType> CurrentUserTypes { get; init; } =
        [new CurrentUserType { Type = "*.ICurrentUser", UserMembers = ["UserId"], TenantMembers = ["TenantId"] }];
    public IReadOnlyList<string> ExtensionMethods { get; init; } = ["GetUserId", "GetTenantId"];
}

/// <summary>A server-side "current user" abstraction whose members are trusted identity values.
/// <see cref="Type"/> may start with "*." to match by simple name.</summary>
public sealed class CurrentUserType
{
    public string Type { get; init; } = "";
    public IReadOnlyList<string> UserMembers { get; init; } = [];
    public IReadOnlyList<string> TenantMembers { get; init; } = [];
}

public sealed class OwnershipOptions
{
    public IReadOnlyList<string> UserOwnerPatterns { get; init; } =
        ["^UserId$", "^OwnerId$", "^OwnerUserId$", "^CreatedBy(Id)?$", "^AuthorId$", "^CustomerId$"];
    public IReadOnlyList<string> TenantOwnerPatterns { get; init; } =
        ["^TenantId$", "^OrganisationId$", "^OrganizationId$", "^CompanyId$"];
    public IReadOnlyList<string> UserTypeNames { get; init; } = ["User", "ApplicationUser", "AppUser", "IdentityUser"];
}

public sealed class InferenceOptions
{
    public string ModelPath { get; init; } = "models/phi3-mini-4k/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4";
    public string ModelId { get; init; } = "phi3-mini-4k-instruct-cpu-int4";
    public int MaxLength { get; init; } = 3072;
    public double SuppressThreshold { get; init; } = 0.80;
    public int RetryOnParseFailure { get; init; } = 1;
    public int MaxSliceTokens { get; init; } = 700;
    public string PromptVersion { get; init; } = "v1";
}
