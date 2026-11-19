using BolaLens.Analysis.Infrastructure;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace BolaLens.Analysis.Taint;

/// <summary>Answers "is this expression derived from the authenticated, server-side identity?":
/// ControllerBase/HttpContext.User, ClaimsPrincipal.FindFirst(Value), configured current-user members
/// (ICurrentUser.UserId/TenantId) and configured extension methods (GetUserId).
/// Limitation: identity stored in fields or caches is not recognised unless the type is configured.</summary>
public sealed class CallerIdentityRecognizer(IdentityOptions options)
{
    public AbstractValue? FromProperty(IPropertyReferenceOperation pr, AbstractValue instance)
    {
        var prop = pr.Property;
        if (prop.Name == "User" && Symbols.FullName(prop.Type) == "System.Security.Claims.ClaimsPrincipal")
            return new AbstractValue { IsPrincipal = true, IdentityExpression = Text(pr) };

        if (instance.Identity is not null && prop.Name == "Value")
            return instance with { IdentityExpression = Text(pr) }; // claim.Value

        var kind = CurrentUserKind(prop.ContainingType, prop.Name)
                   ?? (pr.Instance?.Type is { } it ? CurrentUserKind(it, prop.Name) : null);
        if (kind is not null)
            return new AbstractValue { Identity = kind, IdentityExpression = Text(pr) };

        return null;
    }

    public AbstractValue? FromInvocation(IInvocationOperation inv, AbstractValue receiver, IReadOnlyList<AbstractValue> args)
    {
        var m = inv.TargetMethod;
        if (options.ClaimAccessMethods.Contains(m.Name) && (receiver.IsPrincipal || args.Any(a => a.IsPrincipal)))
        {
            var claim = inv.Arguments.Select(a => a.Value.ConstantValue).FirstOrDefault(c => c.HasValue && c.Value is string).Value as string;
            if (m.Name == "HasClaim") return null;
            return new AbstractValue { Identity = ClaimKind(claim), IdentityExpression = Text(inv) };
        }
        if (options.ExtensionMethods.Contains(m.Name))
        {
            var kind = m.Name.Contains("Tenant", StringComparison.OrdinalIgnoreCase) || m.Name.Contains("Org", StringComparison.OrdinalIgnoreCase)
                ? OwnerKind.Tenant : OwnerKind.User;
            return new AbstractValue { Identity = kind, IdentityExpression = Text(inv) };
        }
        return null;
    }

    public OwnerKind? CurrentUserKind(ITypeSymbol type, string member)
    {
        foreach (var cfg in options.CurrentUserTypes)
        {
            if (!TypeMatches(type, cfg.Type)) continue;
            if (cfg.UserMembers.Contains(member)) return OwnerKind.User;
            if (cfg.TenantMembers.Contains(member)) return OwnerKind.Tenant;
        }
        return null;
    }

    private static bool TypeMatches(ITypeSymbol type, string pattern)
    {
        bool One(ITypeSymbol t) => pattern.StartsWith("*.", StringComparison.Ordinal)
            ? t.Name == pattern[2..]
            : t.ToDisplayString() == pattern;
        if (One(type)) return true;
        foreach (var i in type.AllInterfaces) if (One(i)) return true;
        for (var b = type.BaseType; b is not null; b = b.BaseType) if (One(b)) return true;
        return false;
    }

    /// <summary>Claim type → identity level, judged on the last URI segment only
    /// (ClaimTypes.NameIdentifier is ".../xmlsoap.org/.../nameidentifier" — "org" in the host must not count).</summary>
    public static OwnerKind ClaimKind(string? claim)
    {
        if (claim is null) return OwnerKind.User;
        var name = claim[(claim.LastIndexOf('/') + 1)..].ToLowerInvariant();
        return name.Contains("tenant") || name.StartsWith("org") || name.Contains("organisation") || name.Contains("organization")
               || name.Contains("company") || name == "tid"
            ? OwnerKind.Tenant
            : OwnerKind.User;
    }

    private static string Text(IOperation op) => SourceRefs.Snippet(op.Syntax);
}
