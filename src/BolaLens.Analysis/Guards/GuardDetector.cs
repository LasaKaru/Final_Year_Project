using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.Guards;

/// <summary>An equality-like comparison (or an AuthorizationResult.Succeeded test) inside a condition,
/// with its polarity: <see cref="TrueOnMismatch"/> = the whole condition is true when the two sides differ.</summary>
public sealed record Comparison(IOperation Left, IOperation? Right, bool TrueOnMismatch, bool InConjunction, bool InDisjunction, bool IsResourceCheck);

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
        Walk(condition, negated: false, inAnd: false, inOr: false, list);
        return list;
    }

    private static void Walk(IOperation op, bool negated, bool inAnd, bool inOr, List<Comparison> list)
    {
        op = Unwrap(op);
        switch (op)
        {
            case IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } u:
                Walk(u.Operand, !negated, inAnd, inOr, list);
                break;

            case IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr } b:
                // De Morgan: under negation && behaves as || and vice versa
                var isAnd = (b.OperatorKind == BinaryOperatorKind.ConditionalAnd) ^ negated;
                Walk(b.LeftOperand, negated, inAnd || isAnd, inOr || !isAnd, list);
                Walk(b.RightOperand, negated, inAnd || isAnd, inOr || !isAnd, list);
                break;

            case IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } eq:
                list.Add(new Comparison(eq.LeftOperand, eq.RightOperand, (eq.OperatorKind == BinaryOperatorKind.NotEquals) ^ negated, inAnd, inOr, false));
                break;

            case IInvocationOperation { TargetMethod.Name: "Equals" } inv when inv.Instance is null && inv.Arguments.Length >= 2:
                list.Add(new Comparison(inv.Arguments[0].Value, inv.Arguments[1].Value, negated, inAnd, inOr, false));
                break;

            case IInvocationOperation { TargetMethod.Name: "Equals" } inv when inv.Instance is not null && inv.Arguments.Length >= 1:
                list.Add(new Comparison(inv.Instance, inv.Arguments[0].Value, negated, inAnd, inOr, false));
                break;

            case IPropertyReferenceOperation { Property.Name: "Succeeded" } pr:
                list.Add(new Comparison(pr, null, negated, inAnd, inOr, true));
                break;
        }
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
    public static ExitKind Exit(IOperation? branch)
    {
        switch (branch)
        {
            case null: return ExitKind.None;
            case IReturnOperation: return ExitKind.Return;
            case IThrowOperation: return ExitKind.Throw;
            case IExpressionStatementOperation { Operation: IThrowOperation }: return ExitKind.Throw;
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
