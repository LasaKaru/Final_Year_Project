using System.Text;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace BolaLens.Analysis.Infrastructure;

/// <summary>Converts Roslyn locations/nodes into <see cref="SourceRef"/> records (repo-relative path,
/// 1-based lines) and produces label-free snippets: comments removed, whitespace collapsed,
/// long string literals shortened (prevents comment/label leakage into the SLM prompt, ADR-006).</summary>
public static class SourceRefs
{
    public const int MaxSnippetLength = 220;
    public const int MaxStringLiteral = 40;

    public static SourceRef From(Location loc, string root, string? snippet = null)
    {
        var span = loc.GetLineSpan();
        var file = string.IsNullOrEmpty(span.Path) ? "<memory>" : span.Path;
        var rel = string.IsNullOrEmpty(root) || !Path.IsPathRooted(file) ? file : Path.GetRelativePath(root, file);
        return new SourceRef(rel.Replace('\\', '/'), span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1, snippet);
    }

    public static SourceRef From(SyntaxNode node, string root, bool withSnippet = false) =>
        From(node.GetLocation(), root, withSnippet ? Snippet(node) : null);

    /// <summary>Statement-level reference for an operation's syntax: the enclosing statement
    /// (or the expression body of an expression-bodied member).</summary>
    public static SourceRef Statement(SyntaxNode node, string root)
    {
        var stmt = EnclosingStatement(node);
        var text = stmt is IfStatementSyntax ifs ? IfHeader(ifs) : Snippet(stmt);
        return From(stmt.GetLocation(), root, text);
    }

    public static SyntaxNode EnclosingStatement(SyntaxNode node)
    {
        for (var n = node; n is not null; n = n.Parent)
        {
            switch (n)
            {
                case LambdaExpressionSyntax:
                    // a statement inside a lambda is not the path statement: keep climbing past the lambda
                    continue;
                case StatementSyntax s when s is not BlockSyntax && !IsInsideLambda(s, node):
                    return s;
                case ArrowExpressionClauseSyntax arrow:
                    return arrow.Expression;
                case MemberDeclarationSyntax m:
                    return m;
            }
        }
        return node;
    }

    private static bool IsInsideLambda(SyntaxNode statement, SyntaxNode origin)
    {
        for (var n = origin; n is not null && n != statement; n = n.Parent)
            if (n is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax) return true;
        return false;
    }

    /// <summary>"if (cond) return Forbid();" — the condition plus a short form of the exit/branch.</summary>
    public static string IfHeader(IfStatementSyntax ifs)
    {
        var body = ifs.Statement switch
        {
            BlockSyntax { Statements.Count: 1 } b => Snippet(b.Statements[0]),
            BlockSyntax => "{ … }",
            var s => Snippet(s),
        };
        var text = $"if ({Snippet(ifs.Condition)}) {body}";
        if (ifs.Else is not null) text += " else …";
        return Truncate(text);
    }

    /// <summary>Token-based rendering: drops comments/doc-comments, collapses whitespace, shortens strings.</summary>
    public static string Snippet(SyntaxNode node)
    {
        var sb = new StringBuilder();
        foreach (var token in node.DescendantTokens(descendIntoTrivia: false))
        {
            if (sb.Length > 0 && NeedsSpace(token)) sb.Append(' ');
            sb.Append(token.IsKind(SyntaxKind.StringLiteralToken) && token.Text.Length > MaxStringLiteral + 2
                ? token.Text[..(MaxStringLiteral + 1)] + "…\""
                : token.Text);
        }
        return Truncate(sb.ToString());
    }

    private static bool NeedsSpace(SyntaxToken token)
    {
        var prev = token.GetPreviousToken();
        return prev.TrailingTrivia.Any(t => !t.IsKind(SyntaxKind.SingleLineCommentTrivia) && !t.IsKind(SyntaxKind.MultiLineCommentTrivia) && t.Span.Length > 0)
            || token.LeadingTrivia.Any(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia));
    }

    private static string Truncate(string s) => s.Length <= MaxSnippetLength ? s : s[..(MaxSnippetLength - 1)] + "…";
}
