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

        var membership = MembershipPaths(entities);
        var list = entities.Values.Select(b => b.Build(_owners) with { MembershipPath = membership.GetValueOrDefault(b.Key) })
            .OrderBy(e => e.EntityType, StringComparer.Ordinal).ToList();
        return new OwnershipSchema(list, unresolved);
    }

    /// <summary>Containers are entities users belong to: a collection navigation to the user type (Library.AppUsers), a
    /// collection on the user type (AppUser.Libraries), or a join entity named after them with a user owner
    /// (OrganizationUser: UserId + OrganizationId). Every entity that reaches a container through reference navigations
    /// (≤ 4 hops) or a "{Container}Id" foreign key is membership-scoped. Heuristic: names a relation, not the policy.</summary>
    private Dictionary<string, string> MembershipPaths(Dictionary<string, Builder> entities)
    {
        var userTypes = context.Config.Ownership.UserTypeNames.ToHashSet(StringComparer.Ordinal);
        bool IsUser(ITypeSymbol t) { for (var x = t; x is not null; x = x.BaseType) if (userTypes.Contains(x.Name)) return true; return false; }
        static ITypeSymbol? Element(ITypeSymbol t) =>
            t is INamedTypeSymbol { IsGenericType: true } g && g.AllInterfaces.Any(i => i.Name == "IEnumerable") && g.TypeArguments.Length == 1 ? g.TypeArguments[0] : null;
        Builder? EntityOf(ITypeSymbol t) => entities.GetValueOrDefault(t.OriginalDefinition.ToDisplayString());

        var containers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in entities.Values)
        {
            var props = Symbols.AllProperties(b.Type).ToList();
            if (IsUser(b.Type))
            {
                foreach (var p in props)
                    if (Element(p.Type) is { } el && EntityOf(el) is { } c && !IsUser(el)) containers.Add(c.Key);
                continue;
            }
            if (props.Any(p => Element(p.Type) is { } el && IsUser(el))) containers.Add(b.Key);
            // join entity: OrganizationUser { UserId, OrganizationId / Organization }
            var hasUser = props.Any(p => _owners.KindOfName(p.Name) == OwnerKind.User || IsUser(p.Type));
            if (!hasUser) continue;
            foreach (var p in props.Where(p => !IsUser(p.Type) && EntityOf(p.Type) is not null && Element(p.Type) is null))
            {
                var c = EntityOf(p.Type)!;
                if (b.Type.Name.Contains(c.Type.Name, StringComparison.Ordinal) || b.Type.Name.EndsWith("Member", StringComparison.Ordinal))
                    containers.Add(c.Key);
            }
            foreach (var p in props.Where(p => p.Name.EndsWith("Id", StringComparison.Ordinal) && p.Name.Length > 2))
            {
                var name = p.Name[..^2];
                var c = entities.Values.FirstOrDefault(e => e.Type.Name == name && !IsUser(e.Type));
                if (c is not null && b.Type.Name.Contains(name, StringComparison.Ordinal)) containers.Add(c.Key);
            }
        }

        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in containers) paths[key] = "(container)";
        foreach (var b in entities.Values.Where(b => !containers.Contains(b.Key) && !IsUser(b.Type)))
        {
            // breadth-first over reference navigations
            var queue = new Queue<(ITypeSymbol Type, string Path, int Depth)>([(b.Type, "", 0)]);
            var seen = new HashSet<string>(StringComparer.Ordinal) { b.Key };
            string? found = null;
            while (queue.Count > 0 && found is null)
            {
                var (type, path, depth) = queue.Dequeue();
                foreach (var p in Symbols.AllProperties(type))
                {
                    if (Element(p.Type) is not null || IsUser(p.Type) || EntityOf(p.Type) is not { } target) continue;
                    var next = path.Length == 0 ? p.Name : $"{path}.{p.Name}";
                    if (containers.Contains(target.Key)) { found = next; break; }
                    if (depth < 3 && seen.Add(target.Key)) queue.Enqueue((p.Type, next, depth + 1));
                }
            }
            found ??= Symbols.AllProperties(b.Type).Select(p => p.Name)
                .FirstOrDefault(n => n.EndsWith("Id", StringComparison.Ordinal) && n.Length > 2
                                     && containers.Any(c => entities[c].Type.Name == n[..^2]));
            if (found is not null) paths[b.Key] = found;
        }
        return paths;
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
        public INamedTypeSymbol Type => type;
        public string Key => type.ToDisplayString();
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
