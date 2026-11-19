using BolaLens.Analysis.Infrastructure;
using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.DI;

public enum ResolutionKind { Resolved, Ambiguous, NoSourceImplementation, External, LibraryImplementation }

/// <summary>Result of resolving a call target to analysable source.</summary>
public sealed record Resolution(ResolutionKind Kind, IMethodSymbol? Target, int Candidates, string Reason);

/// <summary>Resolves interface/abstract/virtual call targets to source implementations: an index of every
/// source implementation of every interface/abstract member (cross-project, keyed by documentation id),
/// narrowed by the DI registration map. One → resolved; several → Ambiguous (TC10); none → Unresolved.
/// Limitation: runtime choices (keyed services, factories, IEnumerable&lt;T&gt; selection) stay ambiguous.</summary>
public sealed class ImplementationResolver
{
    private readonly Dictionary<string, List<IMethodSymbol>> _impls = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<IMethodSymbol>> _handlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IMethodSymbol> _libraryImpls = new(StringComparer.Ordinal);
    private readonly DiRegistrationMap _di;

    private readonly List<System.Text.RegularExpressions.Regex> _preferred;

    public ImplementationResolver(AnalysisContext context, DiRegistrationMap di)
    {
        _preferred = context.Config.Analysis.PreferredImplementations.Select(p => new System.Text.RegularExpressions.Regex(p)).ToList();
        _di = di;
        var handlerNames = context.Config.Analysis.HandlerMethods.ToHashSet(StringComparer.Ordinal);
        foreach (var pc in context.Compilations)
        {
            foreach (var type in Symbols.SourceTypes(pc.Compilation.Assembly.GlobalNamespace))
            {
                if (type.TypeKind != TypeKind.Class || type.IsAbstract) continue;
                foreach (var h in type.GetMembers().OfType<IMethodSymbol>().Where(m => handlerNames.Contains(m.Name) && m.Parameters.Length >= 1 && !m.IsStatic))
                {
                    var key = Symbols.Key(h.Parameters[0].Type);
                    if (!_handlers.TryGetValue(key, out var hl)) _handlers[key] = hl = [];
                    hl.Add(h);
                }
                foreach (var iface in type.AllInterfaces)
                {
                    foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
                    {
                        if (type.FindImplementationForInterfaceMember(member) is not IMethodSymbol impl) continue;
                        if (Symbols.IsInSource(impl)) Add(Symbols.Key(member), impl);
                        else _libraryImpls.TryAdd(Symbols.Key(member), impl); // e.g. IAppDbContext.SaveChangesAsync → DbContext.SaveChangesAsync
                    }
                }
                for (var t = type; t is not null; t = t.BaseType)
                {
                    foreach (var method in t.GetMembers().OfType<IMethodSymbol>().Where(m => m.IsOverride))
                    {
                        var most = MostDerivedOverride(type, method);
                        for (var o = method.OverriddenMethod; o is not null; o = o.OverriddenMethod)
                            if (most is not null && Symbols.IsInSource(most)) Add(Symbols.Key(o), most);
                    }
                }
            }
        }
    }

    private static IMethodSymbol? MostDerivedOverride(INamedTypeSymbol type, IMethodSymbol method)
    {
        for (var t = type; t is not null; t = t.BaseType)
            foreach (var m in t.GetMembers(method.Name).OfType<IMethodSymbol>())
                if (m.IsOverride && Overrides(m, method)) return m;
        return null;
    }

    private static bool Overrides(IMethodSymbol m, IMethodSymbol target)
    {
        for (var o = m; o is not null; o = o.OverriddenMethod)
            if (SymbolEqualityComparer.Default.Equals(o.OriginalDefinition, target.OriginalDefinition)) return true;
        return false;
    }

    private void Add(string key, IMethodSymbol impl)
    {
        if (!_impls.TryGetValue(key, out var list)) _impls[key] = list = [];
        if (!list.Any(x => Symbols.Key(x) == Symbols.Key(impl))) list.Add(impl);
    }

    /// <summary>Source handler methods (Handle/HandleAsync/Consume…) whose first parameter is <paramref name="requestType"/>:
    /// the targets of mediator dispatch such as sender.Send(new DeleteCommand(id)).</summary>
    public IReadOnlyList<IMethodSymbol> FindHandlers(ITypeSymbol requestType) =>
        _handlers.TryGetValue(Symbols.Key(requestType), out var l) ? l : [];

    public static bool HasBody(IMethodSymbol m) =>
        !m.IsAbstract && !m.IsExtern && m.DeclaringSyntaxReferences.Length > 0
        && (m.PartialImplementationPart is null || m.PartialImplementationPart.DeclaringSyntaxReferences.Length > 0);

    public Resolution Resolve(IMethodSymbol target)
    {
        target = target.ReducedFrom ?? target;
        var dispatch = target.ContainingType?.TypeKind == TypeKind.Interface || target.IsAbstract || (target.IsVirtual && !target.IsSealed);

        if (!dispatch)
        {
            if (HasBody(target)) return new Resolution(ResolutionKind.Resolved, target, 1, "direct call");
            return Symbols.IsInSource(target)
                ? new Resolution(ResolutionKind.NoSourceImplementation, null, 0, $"no body for {Symbols.Display(target)}")
                : new Resolution(ResolutionKind.External, null, 0, $"external library method {Symbols.Display(target)}");
        }

        var candidates = _impls.TryGetValue(Symbols.Key(target), out var list) ? list.ToList() : [];
        if (target.IsVirtual && HasBody(target) && !candidates.Any(c => Symbols.Key(c) == Symbols.Key(target)))
            candidates.Insert(0, target);

        if (candidates.Count > 1 && target.ContainingType is { } svc)
        {
            var registered = _di.ImplementationsOf(svc);
            if (registered.Count > 0)
            {
                var narrowed = candidates.Where(c => registered.Contains(Symbols.Key(c.ContainingType))).ToList();
                if (narrowed.Count > 0) candidates = narrowed;
            }
        }

        var preferred = false;
        if (candidates.Count > 1 && _preferred.Count > 0)
        {
            var kept = candidates.Where(c => _preferred.Any(r => r.IsMatch(c.ContainingType.ToDisplayString()))).ToList();
            if (kept.Count > 0 && kept.Count < candidates.Count) { candidates = kept; preferred = true; }
        }

        if (candidates.Count == 0 && _libraryImpls.TryGetValue(Symbols.Key(target), out var lib))
            return new Resolution(ResolutionKind.LibraryImplementation, lib, 1, $"{Symbols.Display(target)} is implemented by library method {Symbols.Display(lib)}");

        return candidates.Count switch
        {
            1 => new Resolution(ResolutionKind.Resolved, candidates[0], 1, $"resolved {Symbols.Display(target.ContainingType!)} → {candidates[0].ContainingType.Name}"
                                                                          + (preferred ? " (analysis.preferredImplementations)" : "")),
            0 when Symbols.IsInSource(target) => new Resolution(ResolutionKind.NoSourceImplementation, null, 0,
                $"no source implementation of {Symbols.Display(target)}"),
            0 => new Resolution(ResolutionKind.External, null, 0, $"external dispatch {Symbols.Display(target)}"),
            var n => new Resolution(ResolutionKind.Ambiguous, null, n,
                $"ambiguous dispatch: {n} implementations of {Symbols.Display(target)} ({string.Join(", ", candidates.Select(c => c.ContainingType.Name))})"),
        };
    }
}
