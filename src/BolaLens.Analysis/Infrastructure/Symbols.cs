using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.Infrastructure;

/// <summary>Small, symbol-based helpers (never text-based) shared by all analysers.</summary>
public static class Symbols
{
    public static bool InheritsFrom(ITypeSymbol? type, string metadataName)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (FullName(t.OriginalDefinition) == metadataName) return true;
        return false;
    }

    public static bool InheritsFrom(ITypeSymbol? type, ITypeSymbol? baseType)
    {
        if (baseType is null) return false;
        for (var t = type; t is not null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, baseType.OriginalDefinition)) return true;
        return false;
    }

    /// <summary>"Namespace.Type`N" style metadata name of a (possibly generic) type definition.</summary>
    public static string FullName(ITypeSymbol type)
    {
        var ns = type.ContainingNamespace is { IsGlobalNamespace: false } n ? n.ToDisplayString() + "." : "";
        var outer = type.ContainingType is { } ct ? FullName(ct) + "+" : ns;
        return type is INamedTypeSymbol { Arity: > 0 } nt ? $"{outer}{type.Name}`{nt.Arity}" : $"{outer}{type.Name}";
    }

    public static string Display(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

    public static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "", StringComparison.Ordinal);

    public static string Namespace(ISymbol? s) => s?.ContainingNamespace?.ToDisplayString() ?? "";

    public static bool InNamespace(ISymbol? s, string prefix)
    {
        var ns = Namespace(s);
        return ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
    }

    public static bool IsInSource(ISymbol s) => s.Locations.Any(l => l.IsInSource);

    /// <summary>Stable cross-compilation key (documentation comment id of the original definition).</summary>
    public static string Key(ISymbol s) => s.OriginalDefinition.GetDocumentationCommentId() ?? Display(s.OriginalDefinition);

    public static IEnumerable<INamedTypeSymbol> SourceTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol child)
            {
                foreach (var t in SourceTypes(child)) yield return t;
            }
            else if (member is INamedTypeSymbol type && type.Locations.Any(l => l.IsInSource))
            {
                yield return type;
                foreach (var nested in Nested(type)) yield return nested;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type)
    {
        foreach (var n in type.GetTypeMembers())
        {
            yield return n;
            foreach (var nn in Nested(n)) yield return nn;
        }
    }

    public static IEnumerable<IPropertySymbol> AllProperties(ITypeSymbol type)
    {
        var seen = new HashSet<string>();
        for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
            foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
                if (!p.IsStatic && seen.Add(p.Name)) yield return p;
    }

    /// <summary>True for primitives, string, Guid, DateTime(Offset), TimeSpan, decimal, enums and nullables of those.</summary>
    public static bool IsSimple(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } n)
            return IsSimple(n.TypeArguments[0]);
        if (type.TypeKind == TypeKind.Enum) return true;
        if (type.SpecialType is not SpecialType.None and not SpecialType.System_Object) return true;
        return FullName(type) is "System.Guid" or "System.DateTime" or "System.DateTimeOffset" or "System.TimeSpan"
            or "System.DateOnly" or "System.TimeOnly" or "System.Uri";
    }

    /// <summary>The DbSet&lt;T&gt; element type, or null.</summary>
    public static ITypeSymbol? DbSetElement(ITypeSymbol? type) =>
        type is INamedTypeSymbol { Name: "DbSet", Arity: 1 } nt && InNamespace(nt, "Microsoft.EntityFrameworkCore")
            ? nt.TypeArguments[0]
            : null;
}
