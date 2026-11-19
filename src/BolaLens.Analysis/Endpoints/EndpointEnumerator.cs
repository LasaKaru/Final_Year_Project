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
    /// <summary>Endpoint registrations that could not be analysed (e.g. a Minimal-API handler passed as a variable).</summary>
    public int UnsupportedEndpointStyles { get; private set; }

    public IReadOnlyList<DiscoveredEndpoint> Enumerate(CancellationToken ct = default)
    {
        var result = new List<DiscoveredEndpoint>();
        var seen = new HashSet<string>();
        foreach (var ep in MinimalApis(ct))
            if (seen.Add(ep.Info.Id + "|" + ep.Info.ActionSymbol)) result.Add(ep);

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
        // [Route] and [Authorize] are Inherited = true: a base controller's attributes apply to derived controllers
        var classRoutes = RouteTemplates(InheritedAttributes(controller)).DefaultIfEmpty(null).ToList();
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

    private ParameterInfo Parameter(IParameterSymbol p, IReadOnlySet<string> routeParams, bool minimalApi = false)
    {
        var source = BindingInference.Infer(p, routeParams, minimalApi);
        var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        if (source == BindingSource.Route)
            return new ParameterInfo(p.Name, typeName, source, context.Config.Analysis.RouteParametersAreSelectors || context.IsIdLike(p.Name));
        if (source is BindingSource.Query or BindingSource.Header)
            return new ParameterInfo(p.Name, typeName, source, context.IsIdLike(p.Name));
        if (source == BindingSource.Body && !Symbols.IsSimple(p.Type))
        {
            // ids directly on the DTO (dto.OrderId) and one level down (dto.Tag.Id), as sent by real clients
            var ids = Symbols.AllProperties(p.Type).Where(pr => context.IsIdLike(pr.Name)).Select(pr => $"{p.Name}.{pr.Name}")
                .Concat(Symbols.AllProperties(p.Type)
                    .Where(pr => !Symbols.IsSimple(pr.Type) && pr.Type is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } nt
                                 && !nt.AllInterfaces.Any(i => i.Name == "IEnumerable"))
                    .SelectMany(pr => Symbols.AllProperties(pr.Type).Where(sub => context.IsIdLike(sub.Name)).Select(sub => $"{p.Name}.{pr.Name}.{sub.Name}")))
                .ToList();
            return new ParameterInfo(p.Name, typeName, source, ids.Count > 0, ids.Count > 0 ? string.Join(",", ids) : null);
        }
        return new ParameterInfo(p.Name, typeName, source, source == BindingSource.Body && context.IsIdLike(p.Name));
    }

    /// <summary>Attributes of the controller and of its base classes in the analysed code (framework bases have none that matter).</summary>
    private static IEnumerable<AttributeData> InheritedAttributes(INamedTypeSymbol controller)
    {
        for (var t = controller; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
        {
            if (!ReferenceEquals(t, controller) && !t.Locations.Any(l => l.IsInSource)) yield break;
            foreach (var a in t.GetAttributes()) yield return a;
        }
    }

    private static IEnumerable<string?> RouteTemplates(IEnumerable<AttributeData> attrs) =>
        attrs.Where(a => a.AttributeClass?.Name == "RouteAttribute")
             .Select(a => a.ConstructorArguments.FirstOrDefault().Value as string);

    private AuthorizeInfo Authorization(INamedTypeSymbol controller, IMethodSymbol method)
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

        var c = Read(InheritedAttributes(controller));
        var m = Read(method.GetAttributes());
        var allowAnonymous = m.anon || (c.anon && !m.auth);
        var filters = InheritedAttributes(controller).Concat(method.GetAttributes())
            .Select(a => a.AttributeClass).OfType<INamedTypeSymbol>()
            .Where(IsAccessFilter).Select(t => t.Name).Distinct().ToList();
        return new AuthorizeInfo(c.auth || m.auth, allowAnonymous, [.. c.policies, .. m.policies], [.. c.roles, .. m.roles],
            filters.Count > 0 ? filters : null);
    }

    private static readonly string[] FilterInterfaces = ["IAuthorizationFilter", "IAsyncAuthorizationFilter", "IActionFilter", "IAsyncActionFilter"];

    /// <summary>A filter attribute declared in the analysed code, implementing an MVC authorization/action filter interface,
    /// whose name says it checks access (configurable patterns).</summary>
    private bool IsAccessFilter(INamedTypeSymbol attribute) =>
        attribute.Locations.Any(l => l.IsInSource)
        && attribute.AllInterfaces.Any(i => FilterInterfaces.Contains(i.Name) && Symbols.InNamespace(i, "Microsoft.AspNetCore.Mvc.Filters"))
        && context.Config.Analysis.AccessFilterPatterns.Any(p => System.Text.RegularExpressions.Regex.IsMatch(attribute.Name, p));

    private static HttpVerb VerbOf(INamedTypeSymbol attrClass) => attrClass.Name switch
    {
        "HttpGetAttribute" => HttpVerb.Get,
        "HttpPostAttribute" => HttpVerb.Post,
        "HttpPutAttribute" => HttpVerb.Put,
        "HttpPatchAttribute" => HttpVerb.Patch,
        "HttpDeleteAttribute" => HttpVerb.Delete,
        _ => HttpVerb.Unknown,
    };

    // ------------------------------------------------------------------ Minimal APIs

    private static readonly System.Text.RegularExpressions.Regex MapVerb = new("^Map(Get|Post|Put|Patch|Delete)$");

    /// <summary>Minimal-API endpoints: app.MapPut("/x/{id}", handler), group.MapDelete(Handler, "{id}") (custom wrappers too),
    /// with MapGroup prefixes and RequireAuthorization on the endpoint or its group. A handler that is neither a lambda nor
    /// a method group is counted in <see cref="UnsupportedEndpointStyles"/>.</summary>
    private IEnumerable<DiscoveredEndpoint> MinimalApis(CancellationToken ct)
    {
        foreach (var (tree, _) in context.SourceTrees())
        {
            var model = context.GetModel(tree)!;
            foreach (var node in tree.GetRoot(ct).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (node.Expression is not MemberAccessExpressionSyntax ma || !MapVerb.IsMatch(ma.Name.Identifier.Text)) continue;
                if (model.GetOperation(node, ct) is not IInvocationOperation inv) continue;
                var verb = Enum.Parse<HttpVerb>(MapVerb.Match(ma.Name.Identifier.Text).Groups[1].Value);
                var args = inv.Arguments.Where(a => a.ArgumentKind != ArgumentKind.DefaultValue).Select(a => a.Value).ToList();
                var handler = args.Select(HandlerOf).FirstOrDefault(h => h is not null);
                if (handler is null)
                {
                    UnsupportedEndpointStyles++;
                    continue;
                }
                var template = args.Where(a => a.Type?.SpecialType == SpecialType.System_String).Select(a => a.ConstantValue)
                    .FirstOrDefault(c => c.HasValue && c.Value is string).Value as string ?? "";
                var receiver = inv.Instance ?? (inv.TargetMethod.IsExtensionMethod ? inv.Arguments.FirstOrDefault()?.Value : null);
                var (prefix, groupAuth) = GroupInfo(receiver, node, model, ct);
                var route = (prefix.TrimEnd('/') + "/" + template.TrimStart('/')).Trim('/');
                var routeParams = RouteTemplate.ParameterNames(route);
                var parameters = handler.Parameters.Select(p => Parameter(p, routeParams, minimalApi: true)).ToList();
                var auth = MinimalAuth(handler, node, groupAuth);
                var container = handler.ContainingType?.Name ?? "Program";
                var info = new EndpointInfo(Routes.UnitKey(verb, route), verb, route, container,
                    handler.MethodKind == MethodKind.AnonymousFunction ? $"lambda@{tree.GetLineSpan(node.Span).StartLinePosition.Line + 1}" : handler.Name,
                    Symbols.Display(handler), parameters, auth, context.Ref(node) with { Snippet = SourceRefs.Snippet(node) });
                yield return new DiscoveredEndpoint(info, handler);
            }
        }
    }

    private static IMethodSymbol? HandlerOf(IOperation arg)
    {
        var op = arg;
        while (op is IConversionOperation c) op = c.Operand;
        return op switch
        {
            IDelegateCreationOperation { Target: IMethodReferenceOperation mr } => mr.Method,
            IDelegateCreationOperation { Target: IAnonymousFunctionOperation af } => af.Symbol,
            IAnonymousFunctionOperation af => af.Symbol,
            IMethodReferenceOperation mr => mr.Method,
            _ => null,
        };
    }

    /// <summary>Route prefix from MapGroup("prefix") calls on the receiver chain, and whether the group requires
    /// authorization (RequireAuthorization on the chain or on the same group variable/parameter in the method).
    /// A group passed in as a parameter (convention-based registration) gets the containing class name as prefix.</summary>
    private (string Prefix, bool Auth) GroupInfo(IOperation? receiver, SyntaxNode node, SemanticModel model, CancellationToken ct)
    {
        var parts = new List<string>();
        var auth = false;
        ISymbol? root = null;
        for (var r = receiver; r is not null;)
        {
            while (r is IConversionOperation c) r = c.Operand;
            if (r is IInvocationOperation i)
            {
                if (i.TargetMethod.Name == "MapGroup" && i.Arguments.Select(a => a.Value.ConstantValue).FirstOrDefault(cv => cv.HasValue && cv.Value is string).Value is string g)
                    parts.Insert(0, g.Trim('/'));
                if (i.TargetMethod.Name == "RequireAuthorization") auth = true;
                r = i.Instance ?? (i.TargetMethod.IsExtensionMethod ? i.Arguments.FirstOrDefault()?.Value : null);
                continue;
            }
            root = r switch { ILocalReferenceOperation l => l.Local, IParameterReferenceOperation p => p.Parameter, _ => null };
            break;
        }

        if (root is not null)
        {
            var body = node.FirstAncestorOrSelf<MemberDeclarationSyntax>() ?? node.SyntaxTree.GetRoot(ct);
            foreach (var other in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetOperation(other, ct) is not IInvocationOperation o) continue;
                var recv = o.Instance ?? (o.TargetMethod.IsExtensionMethod ? o.Arguments.FirstOrDefault()?.Value : null);
                while (recv is IConversionOperation c2) recv = c2.Operand;
                var sym = recv switch { ILocalReferenceOperation l => (ISymbol)l.Local, IParameterReferenceOperation p => p.Parameter, _ => null };
                if (SymbolEqualityComparer.Default.Equals(sym, root) && o.TargetMethod.Name == "RequireAuthorization") auth = true;
            }
            if (root is ILocalSymbol local && local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct) is VariableDeclaratorSyntax { Initializer.Value: { } init }
                && model.GetOperation(init, ct) is { } initOp)
            {
                var (p, a) = GroupInfo(initOp, node, model, ct);
                if (p.Length > 0) parts.Insert(0, p);
                auth |= a;
            }
            else if (root is IParameterSymbol { Type.Name: "RouteGroupBuilder" } && node.FirstAncestorOrSelf<ClassDeclarationSyntax>() is { } cls)
            {
                parts.Insert(0, "~" + cls.Identifier.Text); // convention-based group: real prefix decided at runtime
            }
        }
        return (string.Join("/", parts.Where(x => x.Length > 0)), auth);
    }

    private static AuthorizeInfo MinimalAuth(IMethodSymbol handler, SyntaxNode mapCall, bool groupAuth)
    {
        var attrs = handler.GetAttributes();
        var chain = mapCall.Parent?.ToString() ?? "";
        var allowAnon = attrs.Any(a => a.AttributeClass?.Name == "AllowAnonymousAttribute") || chain.Contains(".AllowAnonymous(", StringComparison.Ordinal);
        var hasAuth = groupAuth || attrs.Any(a => a.AttributeClass?.Name == "AuthorizeAttribute") || chain.Contains(".RequireAuthorization(", StringComparison.Ordinal);
        return new AuthorizeInfo(hasAuth, allowAnon, [], []);
    }
}
