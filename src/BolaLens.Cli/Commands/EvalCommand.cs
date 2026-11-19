using System.CommandLine;
using BolaLens.Core.Model;
using BolaLens.Eval;
using BolaLens.Reporting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens eval --report r.json --labels l.json [--compare r2.json …] [--out metrics.md]` — confusion
/// matrix and P/R/F1/FPR/FDR/coverage. A hybrid report yields two arms from the same candidates: symbolic-only
/// (ablation) and hybrid (fused final verdict); --compare adds the arms of further reports (e.g. other models);
/// --baseline-sarif adds the third configuration, a baseline SAST tool mapped to endpoint units.</summary>
public static class EvalCommand
{
    public static Command Create()
    {
        var report = new Option<string>("--report", "-r") { Description = "report.json produced by `scan`", Required = true };
        var labels = new Option<string>("--labels", "-l") { Description = "Frozen labels (e.g. fixtures/BolaLab/expected.json)", Required = true };
        var compare = new Option<string[]>("--compare") { Description = "Further report.json files to add as arms", AllowMultipleArgumentsPerToken = true };
        var output = new Option<string?>("--out", "-o") { Description = "Write the Markdown metrics here" };
        var title = new Option<string?>("--title") { Description = "Heading for the metrics table" };
        var baseline = new Option<string?>("--baseline-sarif") { Description = "SARIF of a baseline SAST tool (e.g. CodeQL) on the same commit: adds a baseline arm" };
        var baselineName = new Option<string>("--baseline-name") { Description = "Name of the baseline arm", DefaultValueFactory = _ => "baseline SAST" };
        var baselineRules = new Option<string[]>("--baseline-rules") { Description = "Only these rule IDs count (default: all results)", AllowMultipleArgumentsPerToken = true };
        var baselineCwes = new Option<string[]>("--baseline-cwe") { Description = "Only results of rules tagged with these CWEs count, e.g. 639 285 862 863", AllowMultipleArgumentsPerToken = true };

        var cmd = new Command("eval", "Evaluate a report against labels") { report, labels, compare, output, title, baseline, baselineName, baselineRules, baselineCwes };
        cmd.SetAction(async (r, ct) =>
        {
            try
            {
                var reports = new List<(string, ScanReport)> { ("report", JsonReportWriter.Read(r.GetValue(report)!)) };
                foreach (var extra in r.GetValue(compare) ?? [])
                    reports.Add((Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(extra))) ?? extra, JsonReportWriter.Read(extra)));
                var labelSet = LabelSet.Load(r.GetValue(labels)!);
                var result = Evaluator.Evaluate(labelSet, reports, r.GetValue(title));
                if (r.GetValue(baseline) is { } sarif)
                {
                    var rules = r.GetValue(baselineRules) is { Length: > 0 } rr ? rr.ToHashSet(StringComparer.Ordinal) : null;
                    var cwes = r.GetValue(baselineCwes) is { Length: > 0 } cc ? cc.Select(c => c.Replace("CWE-", "", StringComparison.OrdinalIgnoreCase).TrimStart('0')).ToHashSet(StringComparer.Ordinal) : null;
                    var arm = BaselineSarif.Arm(r.GetValue(baselineName)!, labelSet, reports[0].Item2, BaselineSarif.Load(sarif, rules, cwes));
                    result = result with { Arms = [.. result.Arms, arm] };
                }
                var md = result.ToMarkdown();
                if (r.GetValue(output) is { } o)
                {
                    var dir = Path.GetDirectoryName(Path.GetFullPath(o));
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(o, md, ct);
                    Console.Error.WriteLine($"wrote {o}");
                }
                Console.WriteLine(md);
                return Common.Ok;
            }
            catch (Exception ex) when (ex is FileNotFoundException or System.Text.Json.JsonException or IOException)
            {
                Common.Error(ex.Message);
                return Common.LoadError;
            }
        });
        return cmd;
    }
}
