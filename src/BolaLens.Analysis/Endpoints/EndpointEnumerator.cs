using BolaLens.Analysis.Infrastructure;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.Endpoints;

/// <summary>An endpoint plus the Roslyn method symbol of its action (kept inside BolaLens.Analysis).</summary>
public sealed record DiscoveredEndpoint(EndpointInfo Info, IMethodSymbol Action);

/// <summary>Enumerates MVC controller actions: verb, combined route, parameters with binding source,
/// id-like parameters and [Authorize]/[AllowAnonymous] (method wins over class).
/// Input: compilations. Limitation: Minimal APIs (app.MapPut(...)) are only *counted* as unsupported.</summary>
public sealed class EndpointEnumerator(AnalysisContext context)
{
    public int UnsupportedEndpointStyles { get; private set; }

    public IReadOnlyList<DiscoveredEndpoint> Enumerate(CancellationToken ct = default)
    {
        var result = new List<DiscoveredEndpoint>();
        var seen = new HashSet<string>();
        UnsupportedEndpointStyles = CountMinimalApis(ct);

        foreach (var pc in context.Compilations)
        {
            var controllerBase = pc.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");
            if (controllerBase is null) continue; // not an MVC project

            foreach (var type in Symbols.SourceTypes(pc.Compilation.Assembly.GlobalNamespace))
            {
                ct.ThrowIfCancellationRequested();
                if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic) continue;
                if (!Symbols.InheritsFrom(type, controllerBase)) continue;
                if (type.GetAttributes().Any(a => a.AttributeClass?.Name == "NonControllerAttribute")) continue;

                foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                {
                    if (method.MethodKind != MethodKind.Ordinary || method.IsStatic || method.DeclaredAccessibility != Accessibility.Public) continue;
                    if (method.GetAttributes().Any(a => a.AttributeClass?.Name == "NonActionAttribute")) continue;
                    foreach (var ep in Describe(type, method))
                        if (seen.Add(ep.Info.Id + "|" + ep.Info.ActionSymbol)) result.Add(ep);
                }
            }
        }
        return result;
    }

    private IEnumerable<DiscoveredEndpoint> Describe(INamedTypeSymbol controller, IMethodSymbol method)
    {
        var controllerName = controller.Name.EndsWith("Controller", StringComparison.Ordinal)
            ? controller.Name[..^"Controller".Length] : controller.Name;
        var classRoutes = RouteTemplates(controller.GetAttributes()).DefaultIfEmpty(null).ToList();
        var methodAttrs = method.GetAttributes();
        var methodRoute = RouteTemplates(methodAttrs).FirstOrDefault();
        var verbs = methodAttrs
            .Where(a => Symbols.InheritsFrom(a.AttributeClass, "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute"))
            .ToList();
        if (verbs.Count == 0) yield break; // conventional actions without verb attributes are out of scope

        var auth = Authorization(controller, method);
        var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        var location = syntax is MethodDeclarationSyntax md
            ? context.Ref(md) with { Snippet = $"{controller.Name}.{method.Name}" }
            : context.Ref(method.Locations[0]);

        foreach (var verbAttr in verbs)
        {
            var verb = VerbOf(verbAttr.AttributeClass!);
            var template = verbAttr.ConstructorArguments.FirstOrDefault().Value as string ?? methodRoute;
            foreach (var classRoute in classRoutes)
            {
                var route = RouteTemplate.Combine(classRoute, template, controllerName, method.Name);
                var routeParams = RouteTemplate.ParameterNames(route);
                var parameters = method.Parameters.Select(p => Parameter(p, routeParams)).ToList();
                var id = Routes.UnitKey(verb, route);
                var info = new EndpointInfo(id, verb, route, controller.Name, method.Name,
                    Symbols.Display(method), parameters, auth, location);
                yield return new DiscoveredEndpoint(info, method);
            }
        }
    }

    private ParameterInfo Parameter(IParameterSymbol p, IReadOnlySet<string> routeParams)
    {
        var source = BindingInference.Infer(p, routeParams);
        var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (source is BindingSource.Route or BindingSource.Query or BindingSource.Header)
            return new ParameterInfo(p.Name, typeName, source, context.IsIdLike(p.Name));
        if (source == BindingSource.Body && !Symbols.IsSimple(p.Type))
        {
            var ids = Symbols.AllProperties(p.Type).Where(pr => context.IsIdLike(pr.Name)).Select(pr => $"{p.Name}.{pr.Name}").ToList();
            return new ParameterInfo(p.Name, typeName, source, ids.Count > 0, ids.Count > 0 ? string.Join(",", ids) : null);
        }
        return new ParameterInfo(p.Name, typeName, source, source == BindingSource.Body && context.IsIdLike(p.Name));
    }

    private static IEnumerable<string?> RouteTemplates(IEnumerable<AttributeData> attrs) =>
        attrs.Where(a => a.AttributeClass?.Name == "RouteAttribute")
             .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string);

    private static AuthorizeInfo Authorization(INamedTypeSymbol controller, IMethodSymbol method)
    {
        static (bool auth, bool anon, List<string> policies, List<string> roles) Read(IEnumerable<AttributeData> attrs)
        {
            bool auth = false, anon = false;
            var policies = new List<string>();
            var roles = new List<string>();
            foreach (var a in attrs)
            {
                if (a.AttributeClass?.Name == "AllowAnonymousAttribute") anon = true;
                if (a.AttributeClass?.Name != "AuthorizeAttribute" && !Symbols.InheritsFrom(a.AttributeClass, "Microsoft.AspNetCore.Authorization.AuthorizeAttribute")) continue;
                auth = true;
                if (a.ConstructorArguments.FirstOrDefault().Value is string ctorPolicy) policies.Add(ctorPolicy);
                foreach (var na in a.NamedArguments)
                {
                    if (na.Key == "Policy" && na.Value.Value is string pol) policies.Add(pol);
                    if (na.Key == "Roles" && na.Value.Value is string r) roles.AddRange(r.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                }
            }
            return (auth, anon, policies, roles);
        }

        var c = Read(controller.GetAttributes());
        var m = Read(method.GetAttributes());
        var allowAnonymous = m.anon || (c.anon && !m.auth);
        return new AuthorizeInfo(c.auth || m.auth, allowAnonymous, [.. c.policies, .. m.policies], [.. c.roles, .. m.roles]);
    }

    private static HttpVerb VerbOf(INamedTypeSymbol attrClass) => attrClass.Name switch
    {
        "HttpGetAttribute" => HttpVerb.Get,
        "HttpPostAttribute" => HttpVerb.Post,
        "HttpPutAttribute" => HttpVerb.Put,
        "HttpPatchAttribute" => HttpVerb.Patch,
        "HttpDeleteAttribute" => HttpVerb.Delete,
        _ => HttpVerb.Unknown,
    };

    private int CountMinimalApis(CancellationToken ct)
    {
        var count = 0;
        foreach (var (tree, compilation) in context.SourceTrees())
        {
            var model = context.GetModel(tree)!;
            foreach (var inv in tree.GetRoot(ct).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (inv.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "MapGet" or "MapPost" or "MapPut" or "MapPatch" or "MapDelete" or "MapMethods" }) continue;
                if (model.GetOperation(inv, ct) is IInvocationOperation op && Symbols.InNamespace(op.TargetMethod, "Microsoft.AspNetCore.Builder"))
                    count++;
            }
        }
        return count;
    }
}
