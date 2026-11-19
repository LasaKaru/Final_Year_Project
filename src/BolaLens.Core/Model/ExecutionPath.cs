namespace BolaLens.Core.Model;

public enum StepKind { Source, Caller, Assignment, Call, Load, Check, Mutation, Guard, Sink, Commit, Unresolved }

/// <summary>One hop of an execution path (in execution order), anchored to source.</summary>
public sealed record PathStep(StepKind Kind, string Description, string? Symbol, SourceRef Location, int Depth = 0);

/// <summary>An annotated path from one client-controlled id parameter of an endpoint to EF Core sinks,
/// across controller → service → repository calls (bounded depth).</summary>
public sealed record ExecutionPath(
    string Id,
    EndpointInfo Endpoint,
    ParameterInfo TaintSource,
    IReadOnlyList<PathStep> Steps,
    IReadOnlyList<SinkInfo> Sinks,
    IReadOnlyList<GuardEvidence> Guards,
    IReadOnlyList<UnresolvedItem> Unresolved,
    bool Truncated,
    bool TaintEnteredUnresolvedCall = false);
