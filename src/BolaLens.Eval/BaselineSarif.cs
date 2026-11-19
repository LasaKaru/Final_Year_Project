using System.Text.Json;
using BolaLens.Core.Model;

namespace BolaLens.Eval;

/// <summary>One result of a baseline SAST tool (from its SARIF file): rule and location.</summary>
public sealed record BaselineResult(string RuleId, string Uri, int StartLine);

/// <summary>Baseline arm (third configuration of the evaluation): maps the SARIF results of another SAST tool (e.g. CodeQL
/// <c>cs/insecure-direct-object-reference</c>) to endpoint units. A unit is flagged when a selected result lies inside the
/// endpoint's action method; otherwise it counts as not flagged (a baseline has no Unknown). Input: SARIF + a BolaLens report
/// of the same commit (used only for the endpoint inventory, never for its verdicts). Limitation: results in services or
/// repositories are not attributed to endpoints (that would need BolaLens's own paths and would favour the baseline).</summary>
public static class BaselineSarif
{
    /// <param name="ruleFilter">Rule IDs to keep (null = all).</param>
    /// <param name="cweFilter">CWE numbers to keep, matched against rule tags such as <c>external/cwe/cwe-639</c> (null = all).</param>
    public static IReadOnlyList<BaselineResult> Load(string path, IReadOnlyCollection<string>? ruleFilter = null, IReadOnlyCollection<string>? cweFilter = null)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var results = new List<BaselineResult>();
        foreach (var run in doc.RootElement.GetProperty("runs").EnumerateArray())
        {
            if (!run.TryGetProperty("results", out var rs)) continue;
            var cwesOf = RuleCwes(run);
            foreach (var r in rs.EnumerateArray())
            {
                var rule = r.TryGetProperty("ruleId", out var rid) ? rid.GetString() ?? "" : "";
                if (rule.Length == 0 && r.TryGetProperty("rule", out var rref) && rref.TryGetProperty("id", out var rrid)) rule = rrid.GetString() ?? "";
                if (ruleFilter is { Count: > 0 } && !ruleFilter.Contains(rule)) continue;
                if (cweFilter is { Count: > 0 } && !(cwesOf.TryGetValue(rule, out var cwes) && cwes.Overlaps(cweFilter))) continue;
                if (!r.TryGetProperty("locations", out var locs)) continue;
                foreach (var l in locs.EnumerateArray())
                {
                    if (!l.TryGetProperty("physicalLocation", out var pl)) continue;
                    var uri = pl.TryGetProperty("artifactLocation", out var al) && al.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "";
                    var line = pl.TryGetProperty("region", out var reg) && reg.TryGetProperty("startLine", out var sl) ? sl.GetInt32() : 0;
                    results.Add(new BaselineResult(rule, Uri.UnescapeDataString(uri).Replace('\\', '/'), line));
                }
            }
        }
        return results;
    }

    /// <summary>ruleId → CWE numbers from the rule tags of the driver and of every extension (query packs).</summary>
    private static Dictionary<string, HashSet<string>> RuleCwes(JsonElement run)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (!run.TryGetProperty("tool", out var tool)) return map;
        var components = new List<JsonElement>();
        if (tool.TryGetProperty("driver", out var driver)) components.Add(driver);
        if (tool.TryGetProperty("extensions", out var ext)) components.AddRange(ext.EnumerateArray());
        foreach (var c in components)
        {
            if (!c.TryGetProperty("rules", out var rules)) continue;
            foreach (var rule in rules.EnumerateArray())
            {
                var id = rule.TryGetProperty("id", out var i) ? i.GetString() : null;
                if (id is null) continue;
                var set = map.TryGetValue(id, out var existing) ? existing : map[id] = new HashSet<string>(StringComparer.Ordinal);
                if (rule.TryGetProperty("properties", out var props) && props.TryGetProperty("tags", out var tags))
                    foreach (var t in tags.EnumerateArray())
                        if (t.GetString() is { } tag && System.Text.RegularExpressions.Regex.Match(tag, @"cwe[-/](\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) is { Success: true } m)
                            set.Add(m.Groups[1].Value.TrimStart('0'));
            }
        }
        return map;
    }

    /// <summary>Unit → prediction (Vulnerable when a result lies in the action method, else Protected).</summary>
    public static IReadOnlyDictionary<string, UnitPrediction> Predict(ScanReport inventory, IReadOnlyList<BaselineResult> results)
    {
        var predictions = new Dictionary<string, UnitPrediction>(StringComparer.Ordinal);
        foreach (var ep in inventory.Findings.Select(f => f.Path.Endpoint).DistinctBy(e => e.Id))
        {
            var key = Routes.UnitKey(ep.Verb, ep.RouteTemplate);
            var file = ep.Location.FilePath.Replace('\\', '/');
            var hits = results.Where(r => SameFile(r.Uri, file) && r.StartLine >= ep.Location.StartLine && r.StartLine <= ep.Location.EndLine)
                .Select(r => r.RuleId).Distinct().ToList();
            var flagged = hits.Count > 0;
            if (predictions.TryGetValue(key, out var existing) && existing.Status == VerdictStatus.Vulnerable) continue;
            predictions[key] = new UnitPrediction(key, flagged ? VerdictStatus.Vulnerable : VerdictStatus.Protected, hits, null);
        }
        return predictions;
    }

    /// <summary>SARIF paths are usually relative to the repository root, BolaLens paths to the solution folder.</summary>
    private static bool SameFile(string sarifUri, string reportPath) =>
        sarifUri.Equals(reportPath, StringComparison.Ordinal)
        || sarifUri.EndsWith("/" + reportPath, StringComparison.Ordinal)
        || reportPath.EndsWith("/" + sarifUri, StringComparison.Ordinal);

    public static EvalArm Arm(string name, LabelSet labels, ScanReport inventory, IReadOnlyList<BaselineResult> results)
    {
        var units = UnitMatcher.Match(labels, Predict(inventory, results));
        var m = ConfusionMatrix.From(units);
        return new EvalArm(name, m, Metrics.From(m), units, IsBaseline: true);
    }
}
