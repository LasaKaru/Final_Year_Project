using BolaLens.Core.Model;

namespace BolaLens.Analysis.Verdict;

/// <summary>Turns an annotated execution path into a three-state symbolic verdict with rule IDs.
/// Input: ExecutionPath + OwnershipSchema (pure C#, no Roslyn). Limitation: it trusts the guard binding
/// computed by the path extractor; a sink's verdict is the first matching rule, the path's is the worst sink.</summary>
public sealed class SymbolicClassifier(OwnershipSchema schema, bool includeReadOnlyPaths = false)
{
    public SymbolicVerdict Classify(ExecutionPath path)
    {
        var sinks = path.Sinks.Where(s => s.IsStateChanging || (includeReadOnlyPaths && s.Kind == SinkKind.Read)).ToList();
        if (sinks.Count == 0)
        {
            return path.TaintEnteredUnresolvedCall || path.Truncated
                ? new SymbolicVerdict(VerdictStatus.Unknown, [SymbolicRules.UnresolvedFlow],
                    $"No state change found, but '{path.TaintSource.Name}' entered unresolved code: {string.Join("; ", path.Unresolved.Select(u => u.Reason).Distinct())}.")
                : new SymbolicVerdict(VerdictStatus.OutOfScope, [SymbolicRules.ReadOnly],
                    $"'{path.TaintSource.Name}' does not reach a state-changing EF Core operation.");
        }

        var perSink = sinks.Select(s => ClassifySink(path, s)).ToList();
        var worst = perSink.MaxBy(v => VerdictOrdering.Rank(v.Status))!;
        var rules = perSink.Where(v => v.Status == worst.Status).SelectMany(v => v.RuleIds).Distinct().ToList();
        return worst with { RuleIds = rules };
    }

    private SymbolicVerdict ClassifySink(ExecutionPath path, SinkInfo sink)
    {
        var entity = schema.Find(sink.EntityType);
        var name = entity?.ShortName ?? sink.EntityType[(sink.EntityType.LastIndexOf('.') + 1)..];
        var valid = sink.ValidGuards.Select(i => path.Guards[i]).ToList();
        var where = $"{sink.Kind} on {name} at {sink.Location}";

        var owner = valid.FirstOrDefault(g => g.Kind is GuardKind.OwnerEquality or GuardKind.LinqOwnerPredicate or GuardKind.ExistenceCheck);
        if (owner is not null)
            return new(VerdictStatus.Protected, [SymbolicRules.OwnerGuard],
                $"{where} is protected: {owner.Kind} binds {name}.{owner.OwnerProperty} to the caller ({owner.CallerExpression}) at {owner.Location}.");

        var resource = valid.FirstOrDefault(g => g.Kind == GuardKind.ResourceAuthorization);
        if (resource is not null)
            return new(VerdictStatus.Protected, [SymbolicRules.ResourceAuthz],
                $"{where} is gated by resource-based authorization at {resource.Location} (delegated to an authorization handler).");

        var invalid = path.Guards.Where(g => !g.IsValid && g.Kind != GuardKind.QueryFilter).Select(g => $"{g.InvalidReason} ({g.Location})").Distinct().ToList();
        var invalidText = invalid.Count > 0 ? $" Rejected checks: {string.Join("; ", invalid)}." : "";

        if (sink.BypassesQueryFilters)
            return new(VerdictStatus.Vulnerable, [SymbolicRules.FilterBypass],
                $"{where}: {name} is loaded with IgnoreQueryFilters() and changed without a valid owner check.{invalidText}");

        var filter = valid.FirstOrDefault(g => g.Kind == GuardKind.QueryFilter);
        var bulk = sink.Kind is SinkKind.BulkUpdate or SinkKind.BulkDelete;
        if (sink.IsDetached || (bulk && !sink.QueryHasOwnerPredicate && filter is null))
            return new(VerdictStatus.Vulnerable, [SymbolicRules.DetachedOrBulk],
                sink.IsDetached
                    ? $"{where}: detached {name} built from the client-controlled key is written without loading or checking ownership.{invalidText}"
                    : $"{where}: bulk write selected by the client-controlled id has no owner predicate.{invalidText}");

        if (filter is not null)
            return new(VerdictStatus.Protected, [SymbolicRules.QueryFilter],
                $"{where} is protected by the {filter.OwnerKind} query filter '{filter.CallerExpression}'.");

        if (entity is null || entity.OwnerCandidates.Count == 0)
            return new(VerdictStatus.Unknown, [SymbolicRules.NoOwnershipModel],
                $"{where}: {name} has no owner candidate, so ownership cannot be checked.");

        var owners = string.Join(", ", entity.OwnerCandidates.Select(o => $"{o.Property} ({o.Kind})"));
        var rejectedFilters = path.Guards.Where(g => g.Kind == GuardKind.QueryFilter && !g.IsValid).Select(g => g.InvalidReason).Distinct().ToList();
        var filterText = rejectedFilters.Count > 0 ? $" Query filter not sufficient: {string.Join("; ", rejectedFilters)}." : "";
        return new(VerdictStatus.Vulnerable, [SymbolicRules.MissingGuard],
            $"{where}: {name} (owners: {owners}) is selected by client-controlled '{path.TaintSource.Name}' and changed with no valid owner guard or applicable query filter.{invalidText}{filterText}");
    }
}
