namespace BolaLens.Analysis.Endpoints;

/// <summary>Combines class and method route templates the way ASP.NET Core attribute routing does
/// ([controller]/[action] tokens, absolute "/" or "~/" method templates).
/// Limitation: conventional (non-attribute) routes and route prefixes from conventions are not modelled.</summary>
public static class RouteTemplate
{
    public static string Combine(string? classTemplate, string? methodTemplate, string controller, string action)
    {
        string result;
        if (!string.IsNullOrEmpty(methodTemplate) && (methodTemplate.StartsWith('/') || methodTemplate.StartsWith("~/", StringComparison.Ordinal)))
            result = methodTemplate.TrimStart('~');
        else if (string.IsNullOrEmpty(classTemplate))
            result = methodTemplate ?? "";
        else if (string.IsNullOrEmpty(methodTemplate))
            result = classTemplate;
        else
            result = classTemplate.TrimEnd('/') + "/" + methodTemplate;

        return ReplaceTokens(result, controller, action).Trim('/');
    }

    public static string ReplaceTokens(string template, string controller, string action) =>
        template.Replace("[controller]", controller, StringComparison.OrdinalIgnoreCase)
                .Replace("[action]", action, StringComparison.OrdinalIgnoreCase);

    /// <summary>Names of the {parameters} in a template (constraints and defaults removed).</summary>
    public static IReadOnlySet<string> ParameterNames(string template)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        while ((i = template.IndexOf('{', i)) >= 0)
        {
            var end = template.IndexOf('}', i);
            if (end < 0) break;
            var inner = template[(i + 1)..end].TrimStart('*');
            var cut = inner.IndexOfAny([':', '=', '?']);
            names.Add(cut >= 0 ? inner[..cut] : inner);
            i = end + 1;
        }
        return names;
    }
}
