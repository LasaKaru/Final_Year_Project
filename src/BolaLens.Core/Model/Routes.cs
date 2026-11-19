using System.Text.RegularExpressions;

namespace BolaLens.Core.Model;

/// <summary>Route normalisation shared by the endpoint enumerator and the evaluation harness:
/// lower-case, no leading/trailing '/', constraints and defaults stripped ({id:int} → {id}).</summary>
public static partial class Routes
{
    [GeneratedRegex(@"\{\s*\*{0,2}([A-Za-z0-9_]+)[^}]*\}")]
    private static partial Regex ParameterRegex();

    public static string Normalise(string route)
    {
        var r = ParameterRegex().Replace(route.Trim(), m => "{" + m.Groups[1].Value + "}");
        return r.Trim('/', '~').ToLowerInvariant();
    }

    public static string UnitKey(HttpVerb verb, string route) => $"{verb.ToString().ToUpperInvariant()} {Normalise(route)}";

    public static string UnitKey(string verb, string route) => $"{verb.Trim().ToUpperInvariant()} {Normalise(route)}";
}
