using BolaLens.Core.Model;

namespace BolaLens.Slicing;

/// <summary>Builds a compact, faithful, label-free IR slice of one execution path: tagged lines
/// ([ENDPOINT] [POLICY] [SCHEMA] [SOURCE] [CALLER] [CALL] [LOAD] [CHECK] [MUTATE] [SINK] [UNRESOLVED])
/// with a source map. Input: ExecutionPath + policy + schema. Limitation: never includes the symbolic
/// verdict, rule IDs or guard validity (keeps the semantic stage independent → fair ablation, ADR-004).</summary>
public sealed class IrSliceGenerator(PolicyMap policy, OwnershipSchema schema, int maxTokens = 700)
{
    public IrSlice Generate(ExecutionPath path)
    {
        var lines = new List<(string Tag, string Text, SourceRef? Source)>();
        var ep = path.Endpoint;
        lines.Add(("ENDPOINT", $"{ep.Verb.ToString().ToUpperInvariant()} {ep.RouteTemplate} -> {ep.Controller}.{ep.Action}  ({Where(ep.Location)})", ep.Location));
        lines.Add(("POLICY", PolicyText(ep, path), null));

        foreach (var entity in EntitiesOf(path))
            lines.Add(("SCHEMA", SchemaText(entity), null));

        var sinkLocations = path.Steps.Where(s => s.Kind == StepKind.Sink).Select(s => s.Location).ToHashSet();
        foreach (var step in path.Steps)
        {
            var (tag, text) = step.Kind switch
            {
                StepKind.Source => ("SOURCE", step.Description),
                StepKind.Caller => ("CALLER", $"{Code(step)}   ({step.Description})"),
                StepKind.Call => ("CALL", Code(step)),
                StepKind.Load => ("LOAD", $"{Code(step)}   ({step.Description})"),
                StepKind.Check => ("CHECK", step.Description is "condition" or "" ? Code(step) : $"{Code(step)}   ({step.Description})"),
                StepKind.Mutation => ("MUTATE", Code(step)),
                StepKind.Sink => ("SINK", $"{Code(step)}   ({step.Description})"),
                StepKind.Commit when !sinkLocations.Contains(step.Location) => ("COMMIT", Code(step)),
                StepKind.Unresolved => ("UNRESOLVED", step.Description),
                _ => ("", ""),
            };
            if (tag.Length == 0) continue;
            lines.Add((tag, $"{Indent(step.Depth)}{text}  ({Where(step.Location)})", step.Location));
        }

        // de-duplicate identical consecutive lines (e.g. one statement producing LOAD twice)
        lines = lines.Where((l, i) => i == 0 || l != lines[i - 1]).ToList();

        var truncated = 0;
        if (Estimate(lines) > maxTokens)
            lines = lines.Where(l => l.Tag != "CALL").ToList(); // hops without effects go first
        while (Estimate(lines) > maxTokens && lines.Count > 6)
        {
            // keep header (endpoint/policy/schema) and the tail (sinks); drop from the middle
            var headerCount = lines.TakeWhile(l => l.Tag is "ENDPOINT" or "POLICY" or "SCHEMA").Count();
            lines.RemoveAt(headerCount + 1);
            truncated++;
        }
        if (truncated > 0) lines.Insert(Math.Min(lines.Count, 3), ("TRUNCATED", $"{truncated} lines omitted", null));

        var ir = lines.Select((l, i) => new IrLine($"L{i + 1}", l.Tag, l.Text, l.Source)).ToList();
        var slice = new IrSlice(path.Id, ir, 0, truncated);
        return slice with { EstimatedTokens = TokenEstimator.Estimate(slice.Text) };
    }

    private static int Estimate(List<(string Tag, string Text, SourceRef? Source)> lines) =>
        TokenEstimator.Estimate(string.Join('\n', lines.Select(l => $"L00 [{l.Tag}] {l.Text}")));

    private static string Indent(int depth) => depth <= 0 ? "" : new string(' ', Math.Min(depth, 5) * 2) + "↳ ";

    private static string Code(PathStep step) => step.Location.Snippet ?? step.Description;

    private static string Where(SourceRef r) => $"{Path.GetFileName(r.FilePath)}:{r.StartLine}";

    private string PolicyText(EndpointInfo ep, ExecutionPath path)
    {
        var a = ep.Authorization;
        var parts = new List<string>
        {
            a.AllowAnonymous ? "[AllowAnonymous]" : a.HasAuthorize
                ? "[Authorize" + (a.Policies.Count > 0 ? $"(Policy={string.Join(",", a.Policies)})" : "") + (a.Roles.Count > 0 ? $"(Roles={string.Join(",", a.Roles)})" : "") + "]"
                : "no [Authorize] attribute",
            $"fallback={(policy.FallbackRequiresAuthenticatedUser ? "authenticated-user" : "none")}",
        };
        if (policy.AuthenticationSchemes.Count > 0) parts.Add($"scheme={string.Join(",", policy.AuthenticationSchemes)}");
        var entities = EntitiesOf(path).Select(e => e.ShortName).ToHashSet();
        foreach (var h in policy.ResourceHandlers.Where(h => entities.Contains(h.ResourceType[(h.ResourceType.LastIndexOf('.') + 1)..])))
            parts.Add($"resource handler {h.HandlerType}({h.RequirementType}, {h.ResourceType[(h.ResourceType.LastIndexOf('.') + 1)..]})");
        return string.Join("; ", parts);
    }

    private IEnumerable<EntityModel> EntitiesOf(ExecutionPath path)
    {
        var names = path.Sinks.Select(s => s.EntityType).ToList();
        foreach (var step in path.Steps.Where(s => s.Kind == StepKind.Load))
        {
            var words = step.Description.Split(' ');
            if (words.Length > 1) names.Add(words[1]);
        }
        return names.Select(schema.Find).Where(e => e is not null).Select(e => e!).DistinctBy(e => e.EntityType);
    }

    private static string SchemaText(EntityModel e)
    {
        var owners = e.OwnerCandidates.Count == 0 ? "none" : string.Join(", ", e.OwnerCandidates.Select(o => $"{o.Property}({o.Kind})"));
        var filters = e.QueryFilters.Count == 0 ? "none" : string.Join("; ", e.QueryFilters.Select(f => $"{f.Expression} [{f.ScopeKind}]"));
        return $"{e.ShortName} owners: {owners}; query filters: {filters}";
    }
}
