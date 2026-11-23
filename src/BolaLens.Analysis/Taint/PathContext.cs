using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.Taint;

/// <summary>A state-changing EF Core operation on an entity token, with its position in execution order.</summary>
public sealed record SinkRecord(SinkKind Kind, EntityToken Token, string Symbol, SourceRef Location, int Seq, bool IsStateChanging);

public enum ComparandClass { Identity, ClientControlled, Other }

/// <summary>A candidate guard found on the path. Its protection window [ActiveFrom, ActiveUntil] is in
/// execution-order sequence numbers: an exit guard protects what follows it (until the end of its method
/// for "return", forever for "throw"); a branch guard protects only the guarded branch.</summary>
public sealed class GuardRecord
{
    public required GuardKind Kind { get; init; }
    public required EntityToken? Token { get; init; }

    /// <summary>For token-less guards (existence checks): every object of this entity type selected by the tracked id.</summary>
    public string? EntityType { get; init; }
    public string? OwnerProperty { get; init; }
    public OwnerKind? OwnerKind { get; init; }
    public ComparandClass Comparand { get; init; }
    public OwnerKind? IdentityKind { get; init; }
    public string? CallerExpression { get; init; }
    public required SourceRef Location { get; init; }
    public int ActiveFrom { get; set; } = int.MaxValue;
    public int ActiveUntil { get; set; } = int.MinValue;
    public bool UntilFrameEnd { get; init; }
    public object? Frame { get; init; }
    public string? StructuralProblem { get; set; }
    public bool AuthForCaller { get; init; } = true;

    /// <summary>The owner value checked belongs to an object built from the request, so the client chose it.</summary>
    public bool OwnerFromRequest { get; init; }

    public bool Covers(int seq) => seq >= ActiveFrom && seq <= ActiveUntil;

    public bool IsActivated => ActiveFrom != int.MaxValue;

    /// <summary>For access checks on the tracked id itself (HasAccessToSeries(uid, seriesId)): provenance tags of that id.</summary>
    public IReadOnlySet<string>? SelectorTags { get; init; }

    /// <summary>Does this guard concern the object of the sink?</summary>
    public bool Concerns(EntityToken token) =>
        Token is not null ? Token.SameObject(token)
        : SelectorTags is { Count: > 0 } tags ? token.TaintSelected && (token.SelectorTags.Overlaps(tags) || token.Aliases.Any(a => a.SelectorTags.Overlaps(tags)))
        : EntityType == token.EntityType && token.TaintSelected;
}

/// <summary>Mutable state of one path walk (one endpoint × one tainted parameter).</summary>
public sealed class PathContext
{
    public int Seq { get; private set; }
    public List<PathStep> Steps { get; } = [];
    public List<SinkRecord> Sinks { get; } = [];
    public List<GuardRecord> Guards { get; } = [];
    public List<UnresolvedItem> Unresolved { get; } = [];
    public List<EntityToken> Tokens { get; } = [];
    public HashSet<string> CallStack { get; } = new(StringComparer.Ordinal);
    public bool TaintEnteredUnresolved { get; set; }
    public bool Truncated { get; set; }
    public int MethodsAnalysed { get; set; }

    public int NextSeq() => ++Seq;

    public int AddStep(StepKind kind, string description, string? symbol, SourceRef location, int depth)
    {
        NextSeq();
        // collapse exact duplicates (same statement, same kind) produced by nested evaluation
        if (Steps.Count > 0 && Steps[^1].Kind == kind && Steps[^1].Location == location && Steps[^1].Description == description)
            return Steps.Count - 1;
        Steps.Add(new PathStep(kind, description, symbol, location, depth));
        return Steps.Count - 1;
    }
}

/// <summary>Per-invocation state: abstract values of parameters/locals, an evaluation cache and `this`.</summary>
public sealed class MethodFrame(IMethodSymbol method, int depth)
{
    public IMethodSymbol Method { get; } = method;
    public int Depth { get; } = depth;
    public Dictionary<ISymbol, AbstractValue> Values { get; } = new(SymbolEqualityComparer.Default);
    public Dictionary<IOperation, AbstractValue> Cache { get; } = new();
    public Dictionary<string, ITypeSymbol> TypeArguments { get; } = new(StringComparer.Ordinal);
    public Stack<AbstractValue> ConditionalAccess { get; } = new();
    public AbstractValue This { get; set; } = AbstractValue.None;
    public AbstractValue Return { get; set; } = AbstractValue.None;
}
