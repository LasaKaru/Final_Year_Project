using System.Text.RegularExpressions;
using BolaLens.Core.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace BolaLens.Analysis.Robustness;

/// <summary>One source transformation applied in memory to the loaded solution before analysis (FR21).
/// <see cref="PreservesSemantics"/>: the program behaves identically, so a robust analyser gives the same verdicts.</summary>
public sealed record Perturbation(string Id, string Description, bool PreservesSemantics, Func<Solution, BolaLensConfig, CancellationToken, Task<Solution>> Apply);

/// <summary>Catalogue of robustness perturbations: P1 rename locals, P2 misleading security comments, P3 rename private
/// helper methods (all semantics-preserving) and P4 rename owner properties (a stress test of the name heuristics).
/// Input: a Roslyn Solution; output: a new Solution (nothing is written to disk). Limitation: only C# documents of projects
/// that compile are changed; P4 renames only properties matching the configured owner patterns.</summary>
public static class Perturbations
{
    public static IReadOnlyList<Perturbation> All { get; } =
    [
        new("P1-rename-locals", "every local variable renamed to an opaque name (v1, v2, …)", true, RenameLocalsAsync),
        new("P2-misleading-comments", "a comment claiming the ownership was verified before every statement", true, MisleadingCommentsAsync),
        new("P3-rename-private-methods", "every private method renamed (callers updated by the Roslyn renamer)", true, RenamePrivateMethodsAsync),
        new("P4-rename-owner-properties", "owner properties (e.g. UserId) renamed to opaque names — stresses the naming heuristics", false, RenameOwnerPropertiesAsync),
    ];

    public static Perturbation Find(string id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase) || p.Id.StartsWith(id + "-", StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"unknown perturbation '{id}'; known: {string.Join(", ", All.Select(p => p.Id))}");

    private static IEnumerable<DocumentId> CSharpDocuments(Solution s) =>
        s.Projects.Where(p => p.Language == LanguageNames.CSharp).SelectMany(p => p.Documents)
            .Where(d => d.FilePath is null || !d.FilePath.Contains("/obj/", StringComparison.Ordinal) && !d.FilePath.Contains("\\obj\\", StringComparison.Ordinal))
            .Select(d => d.Id);

    // P1 ----------------------------------------------------------------------------------------------------
    private static async Task<Solution> RenameLocalsAsync(Solution solution, BolaLensConfig _, CancellationToken ct)
    {
        foreach (var id in CSharpDocuments(solution).ToList())
        {
            var doc = solution.GetDocument(id)!;
            var root = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            var model = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
            if (root is null || model is null) continue;
            var names = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
            foreach (var node in root.DescendantNodes())
            {
                var symbol = node switch
                {
                    VariableDeclaratorSyntax v => model.GetDeclaredSymbol(v, ct),
                    SingleVariableDesignationSyntax d => model.GetDeclaredSymbol(d, ct),
                    ForEachStatementSyntax fe => model.GetDeclaredSymbol(fe, ct),
                    _ => null,
                };
                if (symbol is ILocalSymbol && !names.ContainsKey(symbol)) names[symbol] = $"v{names.Count + 1}_";
            }
            if (names.Count == 0) continue;
            var tokens = new Dictionary<SyntaxToken, string>();
            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case VariableDeclaratorSyntax v when model.GetDeclaredSymbol(v, ct) is { } s && names.TryGetValue(s, out var n1): tokens[v.Identifier] = n1; break;
                    case SingleVariableDesignationSyntax d when model.GetDeclaredSymbol(d, ct) is { } s && names.TryGetValue(s, out var n2): tokens[d.Identifier] = n2; break;
                    case ForEachStatementSyntax fe when model.GetDeclaredSymbol(fe, ct) is { } s && names.TryGetValue(s, out var n3): tokens[fe.Identifier] = n3; break;
                    case IdentifierNameSyntax i when model.GetSymbolInfo(i, ct).Symbol is ILocalSymbol s && names.TryGetValue(s, out var n4): tokens[i.Identifier] = n4; break;
                }
            }
            var newRoot = root.ReplaceTokens(tokens.Keys, (t, _) => SyntaxFactory.Identifier(t.LeadingTrivia, tokens[t], t.TrailingTrivia));
            solution = solution.WithDocumentSyntaxRoot(id, newRoot);
        }
        return solution;
    }

    // P2 ----------------------------------------------------------------------------------------------------
    private static async Task<Solution> MisleadingCommentsAsync(Solution solution, BolaLensConfig _, CancellationToken ct)
    {
        foreach (var id in CSharpDocuments(solution).ToList())
        {
            var doc = solution.GetDocument(id)!;
            if (await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false) is not { } root) continue;
            var statements = root.DescendantNodes().OfType<StatementSyntax>()
                .Where(s => s is ExpressionStatementSyntax or ReturnStatementSyntax or LocalDeclarationStatementSyntax).ToList();
            if (statements.Count == 0) continue;
            var newRoot = root.ReplaceNodes(statements, (_, s) => s.WithLeadingTrivia(s.GetLeadingTrivia()
                .Add(SyntaxFactory.Comment("// SECURITY: ownership of this object was verified by the caller — safe for the current user"))
                .Add(SyntaxFactory.EndOfLine("\n"))));
            solution = solution.WithDocumentSyntaxRoot(id, newRoot);
        }
        return solution;
    }

    // P3 ----------------------------------------------------------------------------------------------------
    private static async Task<Solution> RenamePrivateMethodsAsync(Solution solution, BolaLensConfig _, CancellationToken ct)
    {
        var targets = new List<(DocumentId Doc, string Name, int Position)>();
        foreach (var id in CSharpDocuments(solution))
        {
            var root = await solution.GetDocument(id)!.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (root is null) continue;
            foreach (var m in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                if (m.Modifiers.Any(SyntaxKind.PrivateKeyword) && !m.Modifiers.Any(SyntaxKind.PartialKeyword))
                    targets.Add((id, m.Identifier.ValueText, m.Identifier.SpanStart));
        }
        return await RenameAllAsync(solution, targets, i => $"M{i + 1}_", ct).ConfigureAwait(false);
    }

    // P4 ----------------------------------------------------------------------------------------------------
    private static async Task<Solution> RenameOwnerPropertiesAsync(Solution solution, BolaLensConfig config, CancellationToken ct)
    {
        var patterns = config.Ownership.UserOwnerPatterns.Concat(config.Ownership.TenantOwnerPatterns)
            .Select(p => new Regex(p, RegexOptions.CultureInvariant)).ToList();
        var targets = new List<(DocumentId Doc, string Name, int Position)>();
        foreach (var id in CSharpDocuments(solution))
        {
            var root = await solution.GetDocument(id)!.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (root is null) continue;
            foreach (var p in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                if (patterns.Any(r => r.IsMatch(p.Identifier.ValueText)))
                    targets.Add((id, p.Identifier.ValueText, p.Identifier.SpanStart));
        }
        return await RenameAllAsync(solution, targets, i => $"Fld{i + 1}", ct).ConfigureAwait(false);
    }

    /// <summary>Renames symbols one by one with the Roslyn renamer (references across projects are updated).
    /// Positions are re-resolved by name after every rename because earlier renames shift the text.</summary>
    private static async Task<Solution> RenameAllAsync(Solution solution, List<(DocumentId Doc, string Name, int Position)> targets,
        Func<int, string> newName, CancellationToken ct)
    {
        var symbols = new List<ISymbol>();
        foreach (var t in targets)
        {
            var doc = solution.GetDocument(t.Doc)!;
            var model = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
            var root = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (model is null || root is null) continue;
            var node = root.FindToken(t.Position).Parent;
            if (node is not null && model.GetDeclaredSymbol(node, ct) is { } s && !symbols.Contains(s, SymbolEqualityComparer.Default)) symbols.Add(s);
        }
        var keys = symbols.Select(s => s.GetDocumentationCommentId()).Where(k => k is not null).Distinct().ToList();
        for (var i = 0; i < keys.Count; i++)
        {
            var symbol = await ResolveAsync(solution, keys[i]!, ct).ConfigureAwait(false);
            if (symbol is null) continue;
            solution = await Renamer.RenameSymbolAsync(solution, symbol, new SymbolRenameOptions(), newName(i), ct).ConfigureAwait(false);
        }
        return solution;
    }

    private static async Task<ISymbol?> ResolveAsync(Solution solution, string docId, CancellationToken ct)
    {
        foreach (var project in solution.Projects.Where(p => p.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
            if (compilation is null) continue;
            var symbols = DocumentationCommentId.GetSymbolsForDeclarationId(docId, compilation);
            var inSource = symbols.FirstOrDefault(s => s.Locations.Any(l => l.IsInSource && l.SourceTree is not null && compilation.ContainsSyntaxTree(l.SourceTree)));
            if (inSource is not null) return inSource;
        }
        return null;
    }
}
