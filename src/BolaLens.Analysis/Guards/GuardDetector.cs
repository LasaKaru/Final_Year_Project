using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.Guards;

/// <summary>An equality-like comparison (or an AuthorizationResult.Succeeded test) inside a condition,
/// with its polarity: <see cref="TrueOnMismatch"/> = the whole condition is true when the two sides differ.
/// <see cref="OuterAnd"/>/<see cref="OuterOr"/> are the outermost effective &amp;&amp;/|| nodes above it (De Morgan applied):
/// the group a combined check belongs to (e.g. <c>IsOwner(a.From) || IsOwner(a.To)</c>).</summary>
public sealed record Comparison(IOperation Left, IOperation? Right, bool TrueOnMismatch, bool InConjunction, bool InDisjunction, bool IsResourceCheck, bool IsExistsCheck = false, bool IsHelperCheck = false)
{
    public IOperation? OuterAnd { get; init; }
    public IOperation? OuterOr { get; init; }

    /// <summary>The check does not stop the request on its own: it is OR-ed (match polarity) or AND-ed (mismatch polarity).</summary>
    public bool IsCombined => (TrueOnMismatch && InConjunction) || (!TrueOnMismatch && InDisjunction);

    /// <summary>The node whose other operands can let the request through without this check.</summary>
    public IOperation? CombinationGroup => TrueOnMismatch ? OuterAnd : OuterOr;
}

public enum ExitKind { None, Return, Throw }

/// <summary>Finds candidate guard comparisons in a branch condition: ==/!= (incl. string operators),
/// string.Equals/object.Equals/x.Equals(y), negations, &amp;&amp;/|| structure and `result.Succeeded`.
/// Input: IConditionalOperation condition. Limitation: path-insensitive — it does not use a CFG
/// (S6 replaces the precedes-heuristic with dominance).</summary>
public static class GuardDetector
{
    public static List<Comparison> Collect(IOperation condition)
    {
        var list = new List<Comparison>();
        Walk(condition, negated: false, inAnd: false, inOr: false, null, null, list);
        return list;
    }

    private static void Walk(IOperation op, bool negated, bool inAnd, bool inOr, IOperation? outerAnd, IOperation? outerOr, List<Comparison> list)
    {
        void Add(Comparison c) => list.Add(c with { OuterAnd = outerAnd, OuterOr = outerOr });
        op = Unwrap(op);
        switch (op)
        {
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } u:
                Walk(u.Operand, !negated, inAnd, inOr, outerAnd, outerOr, list);
                break;

            case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } b:
                // De Morgan: under negation && behaves as || and vice versa
                var isAnd = (b.OperatorKind == BinaryOperatorKind.ConditionalAnd) ^ negated;
                var oa = outerAnd ?? (isAnd ? b : null);
                var oo = outerOr ?? (isAnd ? null : b);
                Walk(b.LeftOperand, negated, inAnd || isAnd, inOr || !isAnd, oa, oo, list);
                Walk(b.RightOperand, negated, inAnd || isAnd, inOr || !isAnd, oa, oo, list);
                break;

            case IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } eq:
                Add(new Comparison(eq.LeftOperand, eq.RightOperand, (eq.OperatorKind == BinaryOperatorKind.NotEquals) ^ negated, inAnd, inOr, false));
                break;

            case IInvocationOperation { TargetMethod.Name: "Equals" } inv when inv.Instance is null && inv.Arguments.Length >= 2:
                Add(new Comparison(inv.Arguments[0].Value, inv.Arguments[1].Value, negated, inAnd, inOr, false));
                break;

            case IInvocationOperation { TargetMethod.Name: "Equals" } inv when inv.Instance is not null && inv.Arguments.Length >= 1:
                Add(new Comparison(inv.Instance, inv.Arguments[0].Value, negated, inAnd, inOr, false));
                break;

            case IPropertyReferenceOperation { Property.Name: "Succeeded" } pr:
                Add(new Comparison(pr, null, negated, inAnd, inOr, true));
                break;

            // existence checks: if (!await db.Orders.AnyAsync(o => o.Id == id && o.UserId == uid)) return Forbid();
            // (or a bool local holding that result) — true when the object is the caller's, so negation = mismatch
            case IInvocationOperation { TargetMethod.Name: var n } inv when n.StartsWith("Any", StringComparison.Ordinal):
                Add(new Comparison(inv, null, negated, inAnd, inOr, false, IsExistsCheck: true));
                break;
            // a boolean helper in the analysed code, e.g. if (IsAuthorized(notification.FromUserId)) — summarised by the walker
            case IInvocationOperation { TargetMethod: { ReturnType.SpecialType: SpecialType.System_Boolean } hm } hinv
                when hm.Locations.Any(l => l.IsInSource) && hm.Name != "Equals":
                Add(new Comparison(hinv, null, negated, inAnd, inOr, false, IsHelperCheck: true));
                break;
            case ILocalReferenceOperation or IParameterReferenceOperation when op.Type?.SpecialType == SpecialType.System_Boolean:
                Add(new Comparison(op, null, negated, inAnd, inOr, false, IsExistsCheck: true));
                break;
        }
    }

    /// <summary>Operands of a &amp;&amp;/|| tree with their negation parity relative to the tree's root.</summary>
    public static IEnumerable<(IOperation Leaf, bool Negated)> Leaves(IOperation op, bool negated = false)
    {
        op = Unwrap(op);
        switch (op)
        {
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } u:
                foreach (var l in Leaves(u.Operand, !negated)) yield return l;
                break;
            case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } b:
                foreach (var l in Leaves(b.LeftOperand, negated)) yield return l;
                foreach (var l in Leaves(b.RightOperand, negated)) yield return l;
                break;
            default:
                yield return (op, negated);
                break;
        }
    }

    /// <summary>User.IsInRole("…") / principal.IsInRole(…): a role test (vertical privilege), not an object check.</summary>
    public static bool IsRoleCheck(IOperation leaf) =>
        Unwrap(leaf) is IInvocationOperation { TargetMethod.Name: "IsInRole" } inv
        && (inv.TargetMethod.ContainingType?.Name is "ClaimsPrincipal" or "IPrincipal" || inv.Instance?.Type?.Name == "ClaimsPrincipal");

    /// <summary>Number of operands of a &amp;&amp;/|| tree (negations and parentheses are looked through).</summary>
    public static int CountLeaves(IOperation op)
    {
        op = Unwrap(op);
        return op switch
        {
            IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } u => CountLeaves(u.Operand),
            IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } b =>
                CountLeaves(b.LeftOperand) + CountLeaves(b.RightOperand),
            _ => 1,
        };
    }

    public static IOperation Unwrap(IOperation op)
    {
        while (true)
        {
            switch (op)
            {
                case IConversionOperation c: op = c.Operand; continue;
                case IParenthesizedOperation p: op = p.Operand; continue;
                case IAwaitOperation a: op = a.Operation; continue;
                default: return op;
            }
        }
    }

    /// <summary>Does this branch leave the method (return / throw) at its top level?</summary>
    /// <summary>A throw inside a try block whose catch clause catches the exception and does not rethrow does not leave the
    /// method: execution continues after the try statement (e.g. try { if (o.UserId != uid) throw …; } catch { }).</summary>
    public static bool Swallowed(IThrowOperation thrown)
    {
        var type = thrown.Exception is { } ex ? GuardDetectorTypes.ThrownType(ex) : null;
        for (IOperation? child = thrown, parent = thrown.Parent; parent is not null; child = parent, parent = parent.Parent)
        {
            if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation or IMethodBodyOperation) return false;
            if (parent is not ITryOperation tryOp || !ReferenceEquals(child, tryOp.Body)) continue;
            foreach (var c in tryOp.Catches)
            {
                var catches = type is null || c.ExceptionType.SpecialType == SpecialType.System_Object || GuardDetectorTypes.DerivesFrom(type, c.ExceptionType);
                var rethrows = c.Handler.Descendants().Any(o => o is IThrowOperation);
                if (catches && !rethrows && c.Filter is null) return true;
                if (catches) return false; // the first matching clause handles it
            }
        }
        return false;
    }

    public static ExitKind Exit(IOperation? branch)
    {
        switch (branch)
        {
            case null: return ExitKind.None;
            case IReturnOperation: return ExitKind.Return;
            case IThrowOperation t: return Swallowed(t) ? ExitKind.None : ExitKind.Throw;
            case IExpressionStatementOperation { Operation: IThrowOperation t }: return Swallowed(t) ? ExitKind.None : ExitKind.Throw;
            case IBlockOperation block:
                foreach (var s in block.Operations)
                {
                    var k = Exit(s);
                    if (k != ExitKind.None) return k;
                }
                return ExitKind.None;
            default: return ExitKind.None;
        }
    }
}

internal static class GuardDetectorTypes
{
    public static ITypeSymbol? ThrownType(IOperation exception) =>
        (exception is IConversionOperation c ? c.Operand : exception).Type;

    public static bool DerivesFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t, baseType)) return true;
        return false;
    }
}
