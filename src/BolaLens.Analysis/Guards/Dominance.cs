using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BolaLens.Analysis.Guards;

/// <summary>Syntactic dominance for structured C# (S6): does an exiting check run on every path to a later statement?
/// Input: the check's if-statement and the statement through which the state change is reached in the same method.
/// Limitation: assumes structured control flow — goto/labels and exceptions thrown between the two are not modelled.</summary>
public static class Dominance
{
    /// <summary>True when <paramref name="guard"/> executes before <paramref name="anchor"/> on every path: it sits in a block that
    /// encloses the anchor and precedes it, or in the else-branch of an if whose then-branch always leaves the method.</summary>
    public static bool Dominates(SyntaxNode guard, SyntaxNode anchor)
    {
        var s = guard;
        while (true)
        {
            switch (s.Parent)
            {
                case BlockSyntax or SwitchSectionSyntax when s.Parent.Span.Contains(anchor.Span):
                    return s.Span.End <= anchor.SpanStart;
                // a block that always runs as a whole when execution continues past it: climb to its statement
                // (try body — a swallowed throw is already not an exit —, using, lock, checked, fixed, unsafe, nested { })
                case BlockSyntax block when block.Parent is TryStatementSyntax t && t.Block == block:
                    s = t;
                    continue;
                case BlockSyntax { Parent: UsingStatementSyntax or LockStatementSyntax or CheckedStatementSyntax or FixedStatementSyntax or UnsafeStatementSyntax or BlockSyntax } block:
                    s = block.Parent is BlockSyntax ? block : block.Parent!;
                    continue;
                case ElseClauseSyntax { Parent: IfStatementSyntax outer } when AlwaysExits(outer.Statement):
                    s = outer; // if (x) return …; else if (mismatch) return Forbid(); — the check runs whenever we continue
                    continue;
                default:
                    return false;
            }
        }
    }

    private static bool AlwaysExits(StatementSyntax s) => s switch
    {
        ReturnStatementSyntax or ThrowStatementSyntax => true,
        ExpressionStatementSyntax { Expression: ThrowExpressionSyntax } => true,
        BlockSyntax b => b.Statements.LastOrDefault() is { } last && AlwaysExits(last),
        _ => false,
    };
}
