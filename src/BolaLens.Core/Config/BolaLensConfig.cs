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

    /// <summary>Same configuration with another inference model.</summary>
    public BolaLensConfig WithModel(string modelPath) =>
        new() { Analysis = Analysis, Identity = Identity, Ownership = Ownership, Inference = Inference.WithModel(modelPath) };

    /// <summary>Same configuration with the calibration gate switched on/off.</summary>
    public BolaLensConfig WithGate(bool enabled) =>
        new() { Analysis = Analysis, Identity = Identity, Ownership = Ownership, Inference = Inference.WithGate(enabled) };

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

    /// <summary>Secondary scope (ADR-011): also report loads of objects selected by a client id that are returned
    /// without an owner check (read BOLA / IDOR). Off by default — the primary scope is state-changing BOLA.</summary>
    public bool IncludeReadOnlyPaths { get; init; }

    /// <summary>Custom filter attributes of the analysed code (IAuthorizationFilter/IAsyncAuthorizationFilter/IActionFilter/
    /// IAsyncActionFilter) whose name matches one of these regexes are delegated authorization (SYM-05).</summary>
    public IReadOnlyList<string> AccessFilterPatterns { get; init; } = ["Access", "Authori[sz]", "Permission", "Owner"];

    /// <summary>Roles whose members administer every object (e.g. a host or system administrator role). An endpoint that only
    /// these roles can call is OutOfScope (SYM-10): horizontal BOLA needs peer users. Empty by default — set per target.</summary>
    public IReadOnlyList<string> PrivilegedRoles { get; init; } = [];

    /// <summary>Every route-template parameter identifies a resource ({slug}, {username}, {id}…), not only *Id names.</summary>
    public bool RouteParametersAreSelectors { get; init; } = true;

    /// <summary>Mediator/CQRS dispatch calls (MediatR, Mediator, …): Send(command) is followed into the handler whose
    /// <see cref="HandlerMethods"/> method takes the command type as its first parameter.</summary>
    public IReadOnlyList<string> DispatchMethods { get; init; } = ["Send", "SendAsync", "Publish", "PublishAsync", "Dispatch", "DispatchAsync"];
    public IReadOnlyList<string> HandlerMethods { get; init; } = ["Handle", "HandleAsync", "Consume", "ConsumeAsync"];

    /// <summary>Callee types not followed by the walker (infrastructure such as logging), as regexes on the type name.</summary>
    public IReadOnlyList<string> SkipCalleeTypes { get; init; } = ["Logger$", "LogManager$", "Telemetry"];
    public IReadOnlyList<string> IdParameterPatterns { get; init; } = ["^id$", "Id$", "Key$", "Ids$"];
    public IReadOnlyList<string> ParseLikeMethods { get; init; } = ["Parse", "TryParse", "ToInt32", "ToInt64", "ToString", "ToGuid"];
}

public sealed class IdentityOptions
{
    public IReadOnlyList<string> ClaimsPrincipalMembers { get; init; } = ["User", "HttpContext.User"];
    public IReadOnlyList<string> ClaimAccessMethods { get; init; } = ["FindFirst", "FindFirstValue", "HasClaim"];
    public IReadOnlyList<CurrentUserType> CurrentUserTypes { get; init; } =
    [
        .. new[] { "*.ICurrentUser", "*.ICurrentUserService", "*.ICurrentUserAccessor", "*.ICurrentUserProvider", "*.IUserContext", "*.IUserAccessor", "*.IUser" }
            .Select(t => new CurrentUserType
            {
                Type = t,
                UserMembers = ["UserId", "Id", "PersonId", "Username", "UserName", "GetUserId", "GetCurrentUserId", "GetCurrentPersonId", "GetCurrentUsername", "GetCurrentUserName", "GetUserIdOrThrow", "GetRequiredUserId"],
                TenantMembers = ["TenantId", "OrganizationId", "OrganisationId", "GetTenantId", "GetCurrentTenantId"],
            }),
    ];
    public IReadOnlyList<string> ExtensionMethods { get; init; } = ["GetUserId", "GetTenantId"];

    /// <summary>Members of an object derived from the caller's ClaimsPrincipal (e.g. permissions.GetUser(User).UserId)
    /// that carry the caller's identity.</summary>
    public IReadOnlyList<string> PrincipalDerivedMembers { get; init; } = ["Id", "UserId", "PersonId", "Name", "UserName", "Username", "Email"];
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
        ["UserId$", "^OwnerId$", "^CreatedBy(Id)?$", "^AuthorId$", "^CustomerId$"];
    public IReadOnlyList<string> TenantOwnerPatterns { get; init; } =
        ["^TenantId$", "^OrganisationId$", "^OrganizationId$", "^CompanyId$"];
    public IReadOnlyList<string> UserTypeNames { get; init; } = ["User", "ApplicationUser", "AppUser", "IdentityUser", "Person"];

    /// <summary>Navigation properties that point to the owning user (e.g. Article.Author → Person).</summary>
    public IReadOnlyList<string> OwnerNavigationPatterns { get; init; } = ["^Author$", "^Owner$", "^Creator$", "^User$"];
}

public sealed class InferenceOptions
{
    /// <summary>Default: the tiny model committed to the repository (works offline from a fresh clone).
    /// Research runs pass --model for Phi-3-mini or another provisioned model.</summary>
    public string ModelPath { get; init; } = "models/smollm2-135m-instruct-int4";

    /// <summary>Identifier written into reports.</summary>
    public string ModelId { get; init; } = "smollm2-135m-instruct-int4";

    /// <summary>Upper bound on prompt + output tokens (the model's usable context).</summary>
    public int MaxLength { get; init; } = 4096;

    /// <summary>Output budget per answer (the JSON verdict is short).</summary>
    public int MaxNewTokens { get; init; } = 160;

    public double SuppressThreshold { get; init; } = 0.80;

    /// <summary>Let the SLM turn Unknown into Vulnerable and dispute delegated (SYM-05) protection. Off by default (ADR-012).</summary>
    public bool AllowEscalation { get; init; }
    public int RetryOnParseFailure { get; init; } = 1;
    public int MaxSliceTokens { get; init; } = 700;
    public string PromptVersion { get; init; } = "v1";

    /// <summary>"auto" (from genai_config.json / chat template), "phi3" or "chatml".</summary>
    public string ChatFormat { get; init; } = "auto";

    /// <summary>Constrain decoding to the verdict JSON schema (ONNX Runtime GenAI guidance). Recommended for small models.</summary>
    public bool JsonGuidance { get; init; } = true;

    /// <summary>Test the model on labelled calibration probes first; below <see cref="GateMinCorrect"/> it is advisory only.</summary>
    public bool CalibrationGate { get; init; } = true;

    /// <summary>Probes (of 4: 2 vulnerable, 2 protected) a model must answer correctly; a constant answer scores 2.</summary>
    public int GateMinCorrect { get; init; } = 3;

    /// <summary>Copy with another model folder; the model id becomes the folder name.</summary>
    public InferenceOptions WithModel(string modelPath) => new()
    {
        ModelPath = modelPath,
        ModelId = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(modelPath))),
        MaxLength = MaxLength, MaxNewTokens = MaxNewTokens, SuppressThreshold = SuppressThreshold,
        RetryOnParseFailure = RetryOnParseFailure, MaxSliceTokens = MaxSliceTokens, PromptVersion = PromptVersion,
        ChatFormat = ChatFormat, JsonGuidance = JsonGuidance, CalibrationGate = CalibrationGate, GateMinCorrect = GateMinCorrect, AllowEscalation = AllowEscalation,
    };

    /// <summary>Copy with the calibration gate switched on/off.</summary>
    public InferenceOptions WithGate(bool enabled) => new()
    {
        ModelPath = ModelPath, ModelId = ModelId, MaxLength = MaxLength, MaxNewTokens = MaxNewTokens, SuppressThreshold = SuppressThreshold,
        RetryOnParseFailure = RetryOnParseFailure, MaxSliceTokens = MaxSliceTokens, PromptVersion = PromptVersion,
        ChatFormat = ChatFormat, JsonGuidance = JsonGuidance, CalibrationGate = enabled, GateMinCorrect = GateMinCorrect, AllowEscalation = AllowEscalation,
    };
}
