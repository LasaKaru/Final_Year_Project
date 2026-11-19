using System.Security.Cryptography;
using System.Text;
using BolaLens.Analysis.DI;
using BolaLens.Analysis.Endpoints;
using BolaLens.Analysis.Infrastructure;
using BolaLens.Core.Model;

namespace BolaLens.Analysis.Taint;

/// <summary>For every endpoint parameter that carries a client-controlled id, walks the path and binds
/// guards to sinks: a guard protects a sink only if it concerns the same entity token, compares the
/// owner property with the caller identity of the required owner level, and its protection window
/// covers the sink. Also adds LINQ owner-predicate and query-filter evidence from the schema.
/// Input: endpoints + schema + resolver. Limitation: one path per (endpoint, id parameter).</summary>
public sealed class PathExtractor(AnalysisContext context, OwnershipSchema schema, ImplementationResolver resolver)
{
    private readonly PathWalker _walker = new(context, schema, resolver);

    public IReadOnlyList<ExecutionPath> Extract(IEnumerable<DiscoveredEndpoint> endpoints, CancellationToken ct = default)
    {
        var paths = new List<ExecutionPath>();
        foreach (var ep in endpoints)
        {
            foreach (var source in ep.Info.Parameters.Where(p => p.IsIdLike && p.Source is not BindingSource.Service and not BindingSource.Unknown))
            {
                ct.ThrowIfCancellationRequested();
                PathContext raw;
                try
                {
                    raw = _walker.Walk(ep, source, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    raw = new PathContext();
                    raw.Unresolved.Add(new UnresolvedItem($"analysis error: {ex.GetType().Name}: {ex.Message}", ep.Info.Location));
                    raw.TaintEnteredUnresolved = true;
                }
                paths.Add(Bind(ep.Info, source, raw));
            }
        }
        return paths;
    }

    public ExecutionPath Bind(EndpointInfo endpoint, ParameterInfo source, PathContext raw)
    {
        var guards = new List<GuardEvidence>();
        var sinkGuards = raw.Sinks.Select(_ => new List<int>()).ToList();
        var readMode = context.Config.Analysis.IncludeReadOnlyPaths;
        bool Relevant(SinkRecord s) => s.IsStateChanging || (readMode && s.Kind == SinkKind.Read);
        var stateSinks = raw.Sinks.Where(Relevant).ToList();

        // 1. explicit guards (owner equality, resource authorization)
        foreach (var g in raw.Guards)
        {
            var reason = StaticReason(g);
            var covered = new List<int>();
            for (var i = 0; i < raw.Sinks.Count; i++)
            {
                var s = raw.Sinks[i];
                // reads are exposed when the response is sent, so any executed check on the object protects them
                var inWindow = s.Kind == SinkKind.Read ? g.IsActivated : g.Covers(s.Seq);
                if (Relevant(s) && g.Concerns(s.Token) && inWindow) covered.Add(i);
            }
            if (reason is null && stateSinks.Count > 0 && covered.Count == 0)
            {
                reason = stateSinks.Any(s => g.Concerns(s.Token))
                    ? "guard does not precede the state change"
                    : "guard checks a different object";
            }
            var index = guards.Count;
            guards.Add(new GuardEvidence(g.Kind, reason is null, reason, g.OwnerProperty, g.CallerExpression, g.Location, g.OwnerKind));
            if (reason is null) foreach (var c in covered) sinkGuards[c].Add(index);
        }

        // 2. LINQ owner predicates and 3. query filters — once per entity token that reaches a sink
        foreach (var token in raw.Sinks.Select(s => s.Token).Distinct())
        {
            var entity = schema.Find(token.EntityType);
            var required = entity?.RequiredOwnerKind;
            var sinkIdx = Enumerable.Range(0, raw.Sinks.Count).Where(i => ReferenceEquals(raw.Sinks[i].Token, token)).ToList();

            if (token.OwnerPredicateKind is { } pk)
            {
                string? reason = required == OwnerKind.User && pk != OwnerKind.User ? "tenant-level predicate but object has a user owner" : null;
                var idx = guards.Count;
                guards.Add(new GuardEvidence(GuardKind.LinqOwnerPredicate, reason is null, reason, token.OwnerPredicateProperty,
                    token.OwnerPredicateCaller, token.OwnerPredicateLocation ?? token.LoadedAt, pk));
                if (reason is null) foreach (var i in sinkIdx) sinkGuards[i].Add(idx);
            }

            if (entity is null) continue;
            foreach (var filter in entity.QueryFilters)
            {
                string? reason =
                    token.BypassedFilters ? "query filter bypassed with IgnoreQueryFilters()"
                    : token.Detached ? "detached entity is not loaded through the query filter"
                    : filter.ScopeKind == OwnerKind.Unknown ? "query filter does not reference an owner property"
                    : required == OwnerKind.User && filter.ScopeKind != OwnerKind.User ? "tenant-level check but object has a user owner"
                    : null;
                var idx = guards.Count;
                guards.Add(new GuardEvidence(GuardKind.QueryFilter, reason is null, reason, string.Join(",", filter.ReferencedProperties),
                    filter.Expression, filter.Location, filter.ScopeKind));
                if (reason is null) foreach (var i in sinkIdx) sinkGuards[i].Add(idx);
            }
        }

        var sinks = raw.Sinks.Select((s, i) => new SinkInfo(
            s.Kind, s.Token.EntityType, s.IsStateChanging, s.Token.BypassedFilters, s.Token.Detached,
            s.Token.OwnerPredicateKind is not null, s.Symbol, s.Location, sinkGuards[i])).ToList();

        return new ExecutionPath(PathId(endpoint, source), endpoint, source, raw.Steps.ToList(), sinks, guards,
            raw.Unresolved.ToList(), raw.Truncated, raw.TaintEnteredUnresolved);
    }

    private string? StaticReason(GuardRecord g)
    {
        if (g.OwnerFromRequest)
            return "owner value comes from the request body, not from the stored object";
        if (g.StructuralProblem is not null) return g.StructuralProblem;
        if (g.Kind == Core.Model.GuardKind.ExistenceCheck && g.Comparand == ComparandClass.Other)
            return "existence check does not bind the caller identity";
        if (g.Kind == Core.Model.GuardKind.ResourceAuthorization)
            return g.AuthForCaller ? null : "authorization evaluated for a principal other than the caller";
        switch (g.Comparand)
        {
            case ComparandClass.ClientControlled: return "comparand is client-controlled";
            case ComparandClass.Other: return "comparand is not the caller's identity";
        }
        if (g.IdentityKind is { } ik && g.OwnerKind is { } ok && ik != ok)
            return $"{ik.ToString().ToLowerInvariant()} identity compared with a {ok.ToString().ToLowerInvariant()} owner property";
        var entityType = g.Token?.EntityType ?? g.EntityType;
        var required = entityType is null ? null : schema.Find(entityType)?.RequiredOwnerKind;
        if (required == OwnerKind.User && g.OwnerKind != OwnerKind.User)
            return "tenant-level check but object has a user owner";
        return null;
    }

    public static string PathId(EndpointInfo endpoint, ParameterInfo source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{endpoint.Id}|{endpoint.ActionSymbol}|{source.Name}"));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }
}
