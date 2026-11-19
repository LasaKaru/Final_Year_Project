using BolaLens.Analysis.Infrastructure;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.Schema;

/// <summary>Infers the EF Core ownership schema: DbContexts, DbSet entities, keys, owner candidates and
/// HasQueryFilter filters (in OnModelCreating and IEntityTypeConfiguration&lt;T&gt;.Configure).
/// Input: compilations. Limitation: Fluent configuration beyond HasKey/HasQueryFilter is ignored.</summary>
public sealed class DbContextSchemaMapper(AnalysisContext context)
{
    private readonly OwnerCandidateInference _owners = new(context.Config.Ownership);

    public OwnerCandidateInference Owners => _owners;

    public OwnershipSchema Map(CancellationToken ct = default)
    {
        var entities = new Dictionary<string, Builder>(StringComparer.Ordinal);
        var unresolved = new List<UnresolvedItem>();

        foreach (var pc in context.Compilations)
        {
            var dbContextType = pc.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
            if (dbContextType is null) continue;

            foreach (var type in Symbols.SourceTypes(pc.Compilation.Assembly.GlobalNamespace))
            {
                if (type.TypeKind != TypeKind.Class || !Symbols.InheritsFrom(type.BaseType, dbContextType)) continue;
                foreach (var prop in Symbols.AllProperties(type))
                {
                    if (Symbols.DbSetElement(prop.Type) is INamedTypeSymbol element)
                        Get(entities, element, type.Name).DbSetName ??= prop.Name;
                }
            }
        }

        // modelBuilder.Entity<T>(), HasQueryFilter(...), HasKey(...) anywhere (OnModelCreating or IEntityTypeConfiguration)
        foreach (var (tree, _) in context.SourceTrees())
        {
            var model = context.GetModel(tree)!;
            foreach (var node in tree.GetRoot(ct).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetOperation(node, ct) is not IInvocationOperation inv) continue;
                var m = inv.TargetMethod;
                if (!Symbols.InNamespace(m, "Microsoft.EntityFrameworkCore")) continue;

                if (m.Name == "Entity" && m.TypeArguments.Length == 1 && m.TypeArguments[0] is INamedTypeSymbol et)
                {
                    Get(entities, et, ContextName(node, model)).Touch();
                    continue;
                }

                if (m.Name is not ("HasQueryFilter" or "HasKey")) continue;
                if (m.ContainingType is not { Name: "EntityTypeBuilder", TypeArguments.Length: 1 } builderType
                    || builderType.TypeArguments[0] is not INamedTypeSymbol entityType)
                {
                    unresolved.Add(new UnresolvedItem($"{m.Name} on a non-generic EntityTypeBuilder", context.Ref(node)));
                    continue;
                }

                var lambda = inv.Arguments.SelectMany(a => a.Value.DescendantsAndSelf()).OfType<IAnonymousFunctionOperation>().FirstOrDefault();
                var b = Get(entities, entityType, ContextName(node, model));
                if (lambda is null)
                {
                    if (m.Name == "HasQueryFilter")
                        unresolved.Add(new UnresolvedItem("query filter expression is not an inline lambda", context.Ref(node)));
                    else
                        b.Keys.AddRange(inv.Arguments.Select(a => a.Value.ConstantValue).Where(c => c.HasValue && c.Value is string).Select(c => (string)c.Value!));
                    continue;
                }

                var referenced = ReferencedProperties(lambda);
                if (m.Name == "HasKey")
                {
                    b.Keys.AddRange(referenced);
                    continue;
                }

                var kinds = referenced.Select(_owners.KindOfName).Where(k => k is not null).Select(k => k!.Value).Distinct().ToList();
                var scope = kinds.Contains(OwnerKind.User) ? OwnerKind.User : kinds.Contains(OwnerKind.Tenant) ? OwnerKind.Tenant : OwnerKind.Unknown;
                var text = SourceRefs.Snippet(lambda.Syntax is LambdaExpressionSyntax l ? l.Body : lambda.Syntax);
                b.Filters.Add(new QueryFilterInfo(text, referenced, scope, context.Ref(node)));
            }
        }

        var list = entities.Values.Select(b => b.Build(_owners)).OrderBy(e => e.EntityType, StringComparer.Ordinal).ToList();
        return new OwnershipSchema(list, unresolved);
    }

    private static List<string> ReferencedProperties(IAnonymousFunctionOperation lambda)
    {
        var parameter = lambda.Symbol.Parameters.FirstOrDefault();
        return lambda.Body.Descendants().OfType<IPropertyReferenceOperation>()
            .Where(p => p.Instance is IParameterReferenceOperation pr && SymbolEqualityComparer.Default.Equals(pr.Parameter, parameter))
            .Select(p => p.Property.Name).Distinct().ToList();
    }

    private static string ContextName(SyntaxNode node, SemanticModel model)
    {
        var cls = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        return cls is null ? "?" : model.GetDeclaredSymbol(cls)?.Name ?? cls.Identifier.Text;
    }

    private static Builder Get(Dictionary<string, Builder> map, INamedTypeSymbol entity, string ctx)
    {
        var key = entity.OriginalDefinition.ToDisplayString();
        if (!map.TryGetValue(key, out var b)) map[key] = b = new Builder(entity.OriginalDefinition, ctx);
        return b;
    }

    private sealed class Builder(INamedTypeSymbol type, string dbContext)
    {
        public string? DbSetName { get; set; }
        public List<string> Keys { get; } = [];
        public List<QueryFilterInfo> Filters { get; } = [];

        public void Touch() { }

        public EntityModel Build(OwnerCandidateInference owners)
        {
            var candidates = owners.Infer(type);
            foreach (var f in Filters)
                foreach (var p in f.ReferencedProperties)
                    if (candidates.TryGetValue(p, out var c) && !c.Evidence.Contains(OwnerEvidence.QueryFilter))
                        c.Evidence.Add(OwnerEvidence.QueryFilter);

            var props = Symbols.AllProperties(type).ToList();
            var keys = new List<string>(Keys);
            foreach (var p in props.Where(p => p.GetAttributes().Any(a => a.AttributeClass?.Name == "KeyAttribute")))
                if (!keys.Contains(p.Name)) keys.Add(p.Name);
            if (keys.Count == 0)
            {
                var conv = props.FirstOrDefault(p => p.Name == "Id") ?? props.FirstOrDefault(p => p.Name == type.Name + "Id");
                if (conv is not null) keys.Add(conv.Name);
            }

            return new EntityModel(
                type.ToDisplayString(), dbContext, DbSetName, keys,
                candidates.OrderBy(c => c.Value.Kind).ThenBy(c => c.Key, StringComparer.Ordinal)
                    .Select(c => new OwnerCandidate(c.Key, c.Value.Kind, c.Value.Evidence)).ToList(),
                Filters);
        }
    }
}
