using System.Text.Json;
using System.Text.Json.Nodes;
using BolaLens.Core.Model;

namespace BolaLens.Reporting;

/// <summary>Converts a scan report to SARIF 2.1.0 (GitHub code scanning, IDE viewers): Vulnerable findings are
/// <c>error</c> results, Unknown findings <c>note</c> results of kind <c>review</c>; the EF Core sink is the primary location,
/// the endpoint action a related location and the traced path a code flow. Input: ScanReport + rule descriptions.
/// Limitation: paths are written relative to <c>SRCROOT</c> (the analysed solution's folder), not to the git root.</summary>
public static class SarifReportWriter
{
    public const string SchemaUri = "https://json.schemastore.org/sarif-2.1.0.json";

    /// <param name="uriPrefix">Prepended to every file path, e.g. "src/api/" when the solution is not at the repository root
    /// (GitHub code scanning resolves paths against the repository root).</param>
    public static JsonObject Build(ScanReport report, IReadOnlyDictionary<string, string> ruleDescriptions, bool includeUnknown = true,
        string? srcRoot = null, string? uriPrefix = null)
    {
        var prefix = string.IsNullOrEmpty(uriPrefix) ? "" : uriPrefix.Replace('\\', '/').TrimEnd('/') + "/";
        var selected = report.Findings
            .Where(f => f.Final == VerdictStatus.Vulnerable || (includeUnknown && f.Final == VerdictStatus.Unknown))
            .ToList();
        var ruleIds = ruleDescriptions.Keys.Order(StringComparer.Ordinal).ToList();

        var rules = new JsonArray();
        foreach (var id in ruleIds)
        {
            rules.Add(new JsonObject
            {
                ["id"] = id,
                ["name"] = RuleName(id),
                ["shortDescription"] = new JsonObject { ["text"] = ruleDescriptions[id] },
                ["fullDescription"] = new JsonObject { ["text"] = ruleDescriptions[id] },
                ["help"] = new JsonObject { ["text"] = "Horizontal BOLA: load the object, compare its owner with the authenticated caller before changing it, or add an owner predicate / query filter. See the finding's code flow for the traced path." },
                ["defaultConfiguration"] = new JsonObject { ["level"] = DefaultLevel(id) },
                ["properties"] = new JsonObject
                {
                    ["tags"] = new JsonArray("security", "bola", "idor", "CWE-639"),
                    ["security-severity"] = DefaultLevel(id) == "error" ? "8.1" : "4.0",
                },
            });
        }

        var results = new JsonArray();
        foreach (var f in selected)
        {
            var ruleId = f.Symbolic.RuleIds.FirstOrDefault() ?? "SYM-02";
            // the mutating call (Update/Remove/property write) is more useful than the SaveChanges that commits it
            var sink = f.Path.Sinks.FirstOrDefault(s => s.IsStateChanging && s.Kind != SinkKind.SaveChanges)
                       ?? f.Path.Sinks.FirstOrDefault(s => s.IsStateChanging) ?? f.Path.Sinks.FirstOrDefault();
            var primary = sink?.Location ?? f.Path.Endpoint.Location;
            var vulnerable = f.Final == VerdictStatus.Vulnerable;
            var result = new JsonObject
            {
                ["ruleId"] = ruleId,
                ["ruleIndex"] = Math.Max(0, ruleIds.IndexOf(ruleId)),
                ["level"] = vulnerable ? "error" : "note",
                ["kind"] = vulnerable ? "fail" : "review",
                ["message"] = new JsonObject { ["text"] = Message(f) },
                ["partialFingerprints"] = new JsonObject { ["bolalensFindingId/v1"] = f.FindingId },
                ["properties"] = new JsonObject
                {
                    ["endpoint"] = f.Path.Endpoint.Id,
                    ["taintSource"] = f.Path.TaintSource.Name,
                    ["symbolicStatus"] = f.Symbolic.Status.ToString(),
                    ["finalStatus"] = f.Final.ToString(),
                    ["ruleIds"] = new JsonArray(f.Symbolic.RuleIds.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray()),
                },
            };
            if (primary is not null) result["locations"] = new JsonArray(Location(primary, prefix, sink is null ? "endpoint" : $"{sink.Kind} {Short(sink.EntityType)}"));
            if (f.Path.Endpoint.Location is { } ep && !ReferenceEquals(ep, primary))
            {
                var rel = Location(ep, prefix, $"endpoint {f.Path.Endpoint.Id}");
                rel["id"] = 1;
                result["relatedLocations"] = new JsonArray(rel);
            }
            var flow = new JsonArray();
            foreach (var step in f.Path.Steps)
            {
                flow.Add(new JsonObject
                {
                    ["location"] = Location(step.Location, prefix, $"{step.Kind}: {step.Description}"),
                    ["nestingLevel"] = step.Depth,
                    ["kinds"] = new JsonArray(step.Kind.ToString().ToLowerInvariant()),
                });
            }
            if (flow.Count > 0)
                result["codeFlows"] = new JsonArray(new JsonObject { ["threadFlows"] = new JsonArray(new JsonObject { ["locations"] = flow }) });
            results.Add(result);
        }

        var run = new JsonObject
        {
            ["tool"] = new JsonObject
            {
                ["driver"] = new JsonObject
                {
                    ["name"] = "BolaLens",
                    ["semanticVersion"] = report.Run.ToolVersion,
                    ["informationUri"] = "https://github.com/LasaKaru/Final_Year_Project",
                    ["rules"] = rules,
                },
            },
            ["automationDetails"] = new JsonObject { ["id"] = $"bolalens/{report.Run.Mode}/" },
            ["results"] = results,
            ["columnKind"] = "unicodeCodePoints",
            ["properties"] = new JsonObject
            {
                ["mode"] = report.Run.Mode,
                ["configHash"] = report.Run.ConfigHash,
                ["targetCommit"] = report.Run.TargetCommit,
                ["modelId"] = report.Run.ModelId,
                ["unknownFindingsIncluded"] = includeUnknown,
            },
        };
        if (srcRoot is not null)
            run["originalUriBaseIds"] = new JsonObject { ["SRCROOT"] = new JsonObject { ["uri"] = ToDirectoryUri(srcRoot) } };

        return new JsonObject
        {
            ["$schema"] = SchemaUri,
            ["version"] = "2.1.0",
            ["runs"] = new JsonArray(run),
        };
    }

    public static async Task WriteFileAsync(ScanReport report, IReadOnlyDictionary<string, string> ruleDescriptions, string path,
        bool includeUnknown, string? srcRoot, string? uriPrefix, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = Build(report, ruleDescriptions, includeUnknown, srcRoot, uriPrefix).ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    private static JsonObject Location(SourceRef r, string uriPrefix, string message)
    {
        var region = new JsonObject
        {
            ["startLine"] = Math.Max(1, r.StartLine),
            ["endLine"] = Math.Max(Math.Max(1, r.StartLine), r.EndLine),
        };
        if (r.Snippet is not null) region["snippet"] = new JsonObject { ["text"] = r.Snippet };
        return new JsonObject
        {
            ["physicalLocation"] = new JsonObject
            {
                ["artifactLocation"] = new JsonObject { ["uri"] = uriPrefix + r.FilePath.Replace('\\', '/'), ["uriBaseId"] = "SRCROOT" },
                ["region"] = region,
            },
            ["message"] = new JsonObject { ["text"] = message },
        };
    }

    private static string Message(Finding f)
    {
        var text = $"{f.Path.Endpoint.Id}: {f.Symbolic.Rationale}";
        return f.Semantic is null ? text : $"{text} Semantic stage: {f.FinalReason}";
    }

    private static string Short(string type) => type[(type.LastIndexOf('.') + 1)..];

    private static string DefaultLevel(string ruleId) => ruleId switch
    {
        "SYM-06" or "SYM-07" or "SYM-08" => "error",
        "SYM-02" or "SYM-09" => "note",
        _ => "none",
    };

    private static string RuleName(string ruleId) => ruleId switch
    {
        "SYM-01" => "NoStateChange",
        "SYM-02" => "UnresolvedFlow",
        "SYM-03" => "OwnerGuard",
        "SYM-04" => "OwnerQueryFilter",
        "SYM-05" => "ResourceAuthorization",
        "SYM-06" => "QueryFilterBypassed",
        "SYM-07" => "DetachedOrBulkWrite",
        "SYM-08" => "MissingOwnerCheck",
        "SYM-09" => "NoOwnershipModel",
        "SYM-10" => "PrivilegedRoleOnly",
        _ => ruleId,
    };

    private static string ToDirectoryUri(string dir)
    {
        var full = Path.GetFullPath(dir).Replace('\\', '/');
        if (!full.EndsWith('/')) full += "/";
        return new Uri(full.StartsWith('/') ? "file://" + full : "file:///" + full).AbsoluteUri;
    }
}
