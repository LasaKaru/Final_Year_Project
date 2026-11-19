using BolaLens.Analysis.Infrastructure;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.Policy;

/// <summary>Builds the global policy map from Program.cs/Startup-style registrations: authentication
/// schemes, fallback/default policy, named policies, global authorize filters and resource handlers.
/// Input: all invocations of the solution. Limitation: policies registered from variables/config are
/// reported as Unresolved ("dynamic policy registration"), never guessed.</summary>
public sealed class GlobalPolicyMapper(AnalysisContext context)
{
    private static readonly Dictionary<string, string> SchemeHandlers = new()
    {
        ["AddJwtBearer"] = "JwtBearer",
        ["AddCookie"] = "Cookies",
        ["AddOpenIdConnect"] = "OpenIdConnect",
        ["AddNegotiate"] = "Negotiate",
        ["AddCertificate"] = "Certificate",
        ["AddMicrosoftIdentityWebApi"] = "MicrosoftIdentityWebApi",
        ["AddBearerToken"] = "BearerToken",
        ["AddIdentityCookies"] = "Identity.Application",
    };

    public PolicyMap Map(CancellationToken ct = default)
    {
        var schemes = new List<string>();
        var policies = new List<PolicyDefinition>();
        var unresolved = new List<UnresolvedItem>();
        bool fallbackAuth = false, globalFilter = false;

        foreach (var (tree, _) in context.SourceTrees())
        {
            var model = context.GetModel(tree)!;
            var root = tree.GetRoot(ct);

            foreach (var node in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetOperation(node, ct) is not IInvocationOperation inv) continue;
                var m = inv.TargetMethod;
                if (!Symbols.InNamespace(m, "Microsoft")) continue;

                if (SchemeHandlers.TryGetValue(m.Name, out var scheme))
                {
                    var explicitName = inv.Arguments.Select(a => a.Value.ConstantValue).FirstOrDefault(c => c.HasValue && c.Value is string).Value as string;
                    Add(schemes, explicitName ?? scheme);
                }
                else if (m.Name == "AddScheme" && inv.Arguments.Select(a => a.Value.ConstantValue).FirstOrDefault(c => c.HasValue && c.Value is string).Value is string custom)
                {
                    Add(schemes, custom);
                }

                switch (m.Name)
                {
                    case "SetFallbackPolicy":
                        if (RequiresAuthenticatedUser(inv)) fallbackAuth = true;
                        break;
                    case "AddPolicy" when Symbols.InNamespace(m, "Microsoft.AspNetCore.Authorization") || Symbols.Namespace(m).StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal):
                        var name = inv.Arguments.FirstOrDefault(a => a.Parameter?.Name == "name")?.Value.ConstantValue;
                        if (name is { HasValue: true, Value: string pn})
                            policies.Add(new PolicyDefinition(pn, Requirements(inv), context.Ref(node)));
                        else
                            unresolved.Add(new UnresolvedItem("dynamic policy registration (name is not a constant)", context.Ref(node)));
                        break;
                    case "RequireAuthorization" when IsOnMapControllers(inv):
                        globalFilter = true;
                        break;
                }
            }

            // o.FallbackPolicy = ...; o.Filters.Add(new AuthorizeFilter(...))
            foreach (var node in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetOperation(node, ct) is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.Name: "FallbackPolicy" } } a
                    && a.Value.DescendantsAndSelf().OfType<IInvocationOperation>().Any(i => i.TargetMethod.Name == "RequireAuthenticatedUser"))
                    fallbackAuth = true;
            }
            foreach (var node in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (model.GetOperation(node, ct) is IObjectCreationOperation { Type.Name: "AuthorizeFilter" } oc
                    && EnclosingInvocation(oc) is { TargetMethod.Name: "Add" })
                    globalFilter = true;
            }
        }

        return new PolicyMap(schemes, fallbackAuth, globalFilter, policies, ResourceHandlers(), unresolved);
    }

    private static IInvocationOperation? EnclosingInvocation(IOperation op)
    {
        for (var p = op.Parent; p is not null; p = p.Parent)
        {
            if (p is IInvocationOperation inv) return inv;
            if (p is not (IConversionOperation or IArgumentOperation)) return null;
        }
        return null;
    }

    private static void Add(List<string> list, string value)
    {
        if (!list.Contains(value)) list.Add(value);
    }

    private static bool RequiresAuthenticatedUser(IInvocationOperation inv) =>
        inv.Arguments.Any(a => a.Value.DescendantsAndSelf().OfType<IInvocationOperation>().Any(i => i.TargetMethod.Name == "RequireAuthenticatedUser"));

    private static bool IsOnMapControllers(IInvocationOperation inv)
    {
        var receiver = inv.Instance ?? inv.Arguments.FirstOrDefault()?.Value;
        while (receiver is IConversionOperation c) receiver = c.Operand;
        return receiver is IInvocationOperation { TargetMethod.Name: "MapControllers" or "MapDefaultControllerRoute" or "MapControllerRoute" };
    }

    /// <summary>Requirement calls on the policy builder (RequireRole, RequireClaim, …) and requirement types.</summary>
    private static List<string> Requirements(IInvocationOperation addPolicy)
    {
        var list = new List<string>();
        foreach (var arg in addPolicy.Arguments.Skip(1))
        {
            foreach (var call in arg.Value.DescendantsAndSelf().OfType<IInvocationOperation>())
            {
                if (call.TargetMethod.Name == "AddRequirements")
                {
                    foreach (var oc in call.Arguments.SelectMany(a => a.Value.DescendantsAndSelf()).OfType<IObjectCreationOperation>())
                        if (oc.Type is not null) Add(list, oc.Type.Name);
                }
                else if (call.TargetMethod.Name.StartsWith("Require", StringComparison.Ordinal))
                {
                    var args = call.Arguments.SelectMany(a => a.Value.DescendantsAndSelf())
                        .Select(o => o.ConstantValue).Where(c => c.HasValue && c.Value is string).Select(c => (string)c.Value!).ToList();
                    Add(list, args.Count > 0 ? $"{call.TargetMethod.Name}({string.Join(", ", args)})" : call.TargetMethod.Name);
                }
            }
        }
        return list;
    }

    private List<ResourceHandlerInfo> ResourceHandlers()
    {
        var list = new List<ResourceHandlerInfo>();
        foreach (var pc in context.Compilations)
        {
            foreach (var type in Symbols.SourceTypes(pc.Compilation.Assembly.GlobalNamespace))
            {
                for (var b = type.BaseType; b is not null; b = b.BaseType)
                {
                    if (b is { Name: "AuthorizationHandler", Arity: 2 } && Symbols.InNamespace(b, "Microsoft.AspNetCore.Authorization"))
                    {
                        list.Add(new ResourceHandlerInfo(type.Name, b.TypeArguments[0].Name, b.TypeArguments[1].ToDisplayString(), context.Ref(type.Locations[0])));
                        break;
                    }
                }
            }
        }
        return list;
    }
}
