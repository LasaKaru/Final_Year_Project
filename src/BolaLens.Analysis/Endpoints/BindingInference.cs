using BolaLens.Analysis.Infrastructure;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;

namespace BolaLens.Analysis.Endpoints;

/// <summary>Infers the model-binding source of an action parameter: explicit [From*] attribute, else
/// route (name in template), else simple type → query, else complex type → body.
/// Limitation: [ApiController] inference for IFormFile/CancellationToken is approximated as Service.</summary>
public static class BindingInference
{
    public static BindingSource Infer(IParameterSymbol p, IReadOnlySet<string> routeParams, bool minimalApi = false)
    {
        foreach (var a in p.GetAttributes())
        {
            switch (a.AttributeClass?.Name)
            {
                case "FromRouteAttribute": return BindingSource.Route;
                case "FromBodyAttribute": return BindingSource.Body;
                case "FromQueryAttribute": return BindingSource.Query;
                case "FromHeaderAttribute": return BindingSource.Header;
                case "FromServicesAttribute" or "FromKeyedServicesAttribute": return BindingSource.Service;
                case "FromFormAttribute": return BindingSource.Body;
            }
        }

        var typeName = Symbols.FullName(p.Type);
        if (typeName is "System.Threading.CancellationToken" or "Microsoft.AspNetCore.Http.HttpContext" or "System.Security.Claims.ClaimsPrincipal"
            or "Microsoft.AspNetCore.Http.HttpRequest" or "Microsoft.AspNetCore.Http.HttpResponse")
            return BindingSource.Service;
        // Minimal APIs inject interfaces/abstract classes (ISender, IMediator, IRepository…) and DbContexts from DI.
        if (minimalApi && (p.Type.TypeKind == TypeKind.Interface || p.Type.IsAbstract || Symbols.InheritsFrom(p.Type, "Microsoft.EntityFrameworkCore.DbContext")))
            return BindingSource.Service;
        if (routeParams.Contains(p.Name)) return BindingSource.Route;
        return Symbols.IsSimple(p.Type) ? BindingSource.Query : BindingSource.Body;
    }
}
