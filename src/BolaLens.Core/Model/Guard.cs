namespace BolaLens.Core.Model;

public enum GuardKind { OwnerEquality, LinqOwnerPredicate, ResourceAuthorization, QueryFilter, ExistenceCheck, Unknown }

/// <summary>Evidence of an authorization check. Invalid guards are kept (with a reason) because they are
/// the best explanations: "comparand is client-controlled", "guard checks a different object", …</summary>
public sealed record GuardEvidence(
    GuardKind Kind,
    bool IsValid,
    string? InvalidReason,
    string? OwnerProperty,
    string? CallerExpression,
    SourceRef Location,
    OwnerKind? OwnerKind = null);
