using System.Text.RegularExpressions;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.Infrastructure;

/// <summary>Everything the analysers share for one scan: the solution, one compilation per C# project
/// (multi-targeted projects de-duplicated by file path), a SemanticModel cache and the config.
/// Limitation: only C# projects are analysed; generated files (obj/, *.g.cs) are skipped.</summary>
public sealed class AnalysisContext
{
    private readonly Dictionary<SyntaxTree, Compilation> _treeOwner = new();
    private readonly Dictionary<SyntaxTree, SemanticModel> _models = new();
    private readonly Regex[] _idPatterns;

    private AnalysisContext(Solution solution, string root, BolaLensConfig config, IReadOnlyList<ProjectCompilation> compilations)
    {
        Solution = solution;
        RootDirectory = root;
        Config = config;
        Compilations = compilations;
        foreach (var pc in compilations)
            foreach (var tree in pc.Compilation.SyntaxTrees)
                _treeOwner.TryAdd(tree, pc.Compilation);
        _idPatterns = config.Analysis.IdParameterPatterns.Select(p => new Regex(p, RegexOptions.CultureInvariant)).ToArray();
    }

    public Solution Solution { get; }
    public string RootDirectory { get; }
    public BolaLensConfig Config { get; }
    public IReadOnlyList<ProjectCompilation> Compilations { get; }

    public static async Task<AnalysisContext> CreateAsync(Solution solution, string root, BolaLensConfig config, CancellationToken ct)
    {
        var list = new List<ProjectCompilation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects.Where(p => p.Language == LanguageNames.CSharp))
        {
            var key = project.FilePath ?? project.Name;
            if (!seen.Add(key)) continue; // multi-targeting: analyse the first TFM only
            var compilation = await project.GetCompilationAsync(ct).ConfigureAwait(false);
            if (compilation is null) continue;
            var errors = compilation.GetDiagnostics(ct).Count(d => d.Severity == DiagnosticSeverity.Error);
            list.Add(new ProjectCompilation(project, compilation, errors));
        }
        return new AnalysisContext(solution, root, config, list);
    }

    /// <summary>Semantic model for any tree of the solution (cached per tree).</summary>
    public SemanticModel? GetModel(SyntaxTree tree)
    {
        if (_models.TryGetValue(tree, out var m)) return m;
        if (!_treeOwner.TryGetValue(tree, out var compilation)) return null;
        m = compilation.GetSemanticModel(tree);
        _models[tree] = m;
        return m;
    }

    public bool IsIdLike(string name) => _idPatterns.Any(r => r.IsMatch(name));

    public IEnumerable<(SyntaxTree Tree, Compilation Compilation)> SourceTrees() =>
        Compilations.SelectMany(pc => pc.Compilation.SyntaxTrees
            .Where(t => !IsGenerated(t.FilePath))
            .Select(t => (t, pc.Compilation)));

    public SourceRef Ref(SyntaxNode node, bool snippet = false) => SourceRefs.From(node, RootDirectory, snippet);

    public SourceRef Ref(Location location) => SourceRefs.From(location, RootDirectory);

    public SourceRef StatementRef(SyntaxNode node) => SourceRefs.Statement(node, RootDirectory);

    private static bool IsGenerated(string? path) =>
        path is not null && (path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/obj/", StringComparison.OrdinalIgnoreCase));
}

/// <summary>A loaded project, its compilation and how many compile errors it has
/// (many errors = unresolved references = unreliable analysis).</summary>
public sealed record ProjectCompilation(Project Project, Compilation Compilation, int ErrorCount);
