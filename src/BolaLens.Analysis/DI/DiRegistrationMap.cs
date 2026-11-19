using BolaLens.Analysis.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.DI;

/// <summary>Maps service types to implementation types from AddScoped/AddTransient/AddSingleton
/// (generic and typeof forms, incl. open generics). Input: all invocations of the solution.
/// Limitation: factory registrations (AddScoped&lt;I&gt;(sp =&gt; …)) and assembly scanning are not resolved.</summary>
public sealed class DiRegistrationMap
{
    private static readonly HashSet<string> Methods =
        ["AddScoped", "AddTransient", "AddSingleton", "TryAddScoped", "TryAddTransient", "TryAddSingleton",
         "AddKeyedScoped", "AddKeyedTransient", "AddKeyedSingleton"];

    private readonly Dictionary<string, List<(string Key, string Display)>> _map = new(StringComparer.Ordinal);

    public static DiRegistrationMap Build(AnalysisContext context, CancellationToken ct = default)
    {
        var map = new DiRegistrationMap();
        foreach (var (tree, _) in context.SourceTrees())
        {
            var model = context.GetModel(tree)!;
            foreach (var node in tree.GetRoot(ct).DescendantNodes().OfType<InvocationExpressionSyntax>())
                if (model.GetOperation(node, ct) is IInvocationOperation inv) map.Collect(inv);
        }
        return map;
    }

    public void Collect(IInvocationOperation inv)
    {
        var m = inv.TargetMethod;
        if (!Methods.Contains(m.Name)) return;
        if (!Symbols.Namespace(m).StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal)) return;

        if (m.TypeArguments.Length == 2 && m.TypeArguments[0] is INamedTypeSymbol svc && m.TypeArguments[1] is INamedTypeSymbol impl)
        {
            Add(svc, impl);
            return;
        }
        var typeofs = inv.Arguments.Select(a => a.Value).OfType<ITypeOfOperation>().ToList();
        if (typeofs.Count == 2 && typeofs[0].TypeOperand is INamedTypeSymbol s && typeofs[1].TypeOperand is INamedTypeSymbol i)
            Add(s, i);
    }

    private void Add(INamedTypeSymbol service, INamedTypeSymbol impl)
    {
        var key = Symbols.Key(service);
        if (!_map.TryGetValue(key, out var list)) _map[key] = list = [];
        var implKey = Symbols.Key(impl);
        if (list.All(x => x.Key != implKey)) list.Add((implKey, impl.OriginalDefinition.ToDisplayString()));
        _displayNames[key] = service.OriginalDefinition.ToDisplayString();
    }

    private readonly Dictionary<string, string> _displayNames = new(StringComparer.Ordinal);

    /// <summary>Implementation type keys registered for a service type (empty if not registered).</summary>
    public IReadOnlyList<string> ImplementationsOf(INamedTypeSymbol service) =>
        _map.TryGetValue(Symbols.Key(service), out var l) ? l.Select(x => x.Key).ToList() : [];

    /// <summary>Human-readable map for the CLI / tests: service → implementations.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ToDisplayMap() =>
        _map.ToDictionary(kv => _displayNames[kv.Key], kv => (IReadOnlyList<string>)kv.Value.Select(v => v.Display).ToList());
}
