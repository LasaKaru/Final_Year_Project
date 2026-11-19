using BolaLens.Analysis.Infrastructure;
using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.Sinks;

public enum EfCategory
{
    None,
    SetAccess,      // DbContext.Set<T>()
    Chain,          // Where, Include, OrderBy, AsNoTracking, …
    Bypass,         // IgnoreQueryFilters
    Load,           // Find*, First*, Single*, Last*
    Materialize,    // ToList*, ToArray*, AsEnumerable, …
    Exists,         // Any*, Count*, All*
    Write,          // Update, Remove, Attach (+Range)
    Create,         // Add, AddAsync, AddRange — out of primary scope
    Commit,         // SaveChanges*
    BulkUpdate,     // ExecuteUpdate*
    BulkDelete,     // ExecuteDelete*
    EntryAccess,    // Entry(x), .Property(..), .Reference(..)
}

/// <summary>Classifies EF Core / IQueryable operations by the *resolved method symbol* (never by text):
/// namespace Microsoft.EntityFrameworkCore or System.Linq.Queryable. Input: IMethodSymbol.
/// Limitation: repository wrappers are handled by inter-procedural analysis, not by this classifier.</summary>
public static class EfOperationClassifier
{
    public static bool IsEf(IMethodSymbol m) => Symbols.InNamespace(m, "Microsoft.EntityFrameworkCore");

    public static bool IsQueryable(IMethodSymbol m) => m.ContainingType?.ToDisplayString() == "System.Linq.Queryable";

    /// <summary>LINQ-to-objects; only meaningful when applied to a loaded entity's collection navigation
    /// (article.Comments.First(c =&gt; c.Id == id)) — the walker ignores it on anything else.</summary>
    public static bool IsEnumerable(IMethodSymbol m) => m.ContainingType?.ToDisplayString() == "System.Linq.Enumerable";

    public static EfCategory Classify(IMethodSymbol method)
    {
        var m = method.ReducedFrom ?? method;
        var ef = IsEf(m);
        var q = IsQueryable(m) || IsEnumerable(m);
        if (!ef && !q) return EfCategory.None;
        var name = m.Name;

        if (ef && name == "Set" && m.TypeArguments.Length == 1) return EfCategory.SetAccess;
        if (name == "IgnoreQueryFilters") return EfCategory.Bypass;
        if (name is "ExecuteUpdate" or "ExecuteUpdateAsync") return EfCategory.BulkUpdate;
        if (name is "ExecuteDelete" or "ExecuteDeleteAsync") return EfCategory.BulkDelete;
        if (ef && name is "SaveChanges" or "SaveChangesAsync") return EfCategory.Commit;
        if (ef && name is "Update" or "UpdateRange" or "Remove" or "RemoveRange" or "Attach" or "AttachRange") return EfCategory.Write;
        if (ef && name is "Add" or "AddAsync" or "AddRange" or "AddRangeAsync") return EfCategory.Create;
        if (ef && name is "Entry" or "Property" or "Reference" or "Collection" or "Member" or "Navigation") return EfCategory.EntryAccess;

        var baseName = name.EndsWith("Async", StringComparison.Ordinal) ? name[..^5] : name;
        if (baseName is "Find" or "First" or "FirstOrDefault" or "Single" or "SingleOrDefault" or "Last" or "LastOrDefault" or "ElementAt" or "ElementAtOrDefault")
            return EfCategory.Load;
        if (baseName is "ToList" or "ToArray" or "ToDictionary" or "ToHashSet" or "AsEnumerable" or "AsAsyncEnumerable" or "Load")
            return EfCategory.Materialize;
        if (baseName is "Any" or "All" or "Count" or "LongCount" or "Contains")
            return EfCategory.Exists;
        if (baseName is "Where" or "Include" or "ThenInclude" or "AsNoTracking" or "AsTracking" or "AsNoTrackingWithIdentityResolution"
            or "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" or "Skip" or "Take" or "AsSplitQuery"
            or "AsSingleQuery" or "TagWith" or "Distinct" or "AsQueryable")
            return EfCategory.Chain;
        return EfCategory.None;
    }
}
