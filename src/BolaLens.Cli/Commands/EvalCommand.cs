using System.CommandLine;
using BolaLens.Core.Model;
using BolaLens.Eval;
using BolaLens.Reporting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens eval --report r.json --labels l.json [--compare r2.json …] [--out metrics.md]` — confusion
/// matrix and P/R/F1/FPR/FDR/coverage. A hybrid report yields two arms from the same candidates: symbolic-only
/// (ablation) and hybrid (fused final verdict); --compare adds the arms of further reports (e.g. other models).</summary>
public static class EvalCommand
{
    public static Command Create()
    {
        var report = new Option<string>("--report", "-r") { Description = "report.json produced by `scan`", Required = true };
        var labels = new Option<string>("--labels", "-l") { Description = "Frozen labels (e.g. fixtures/BolaLab/expected.json)", Required = true };
        var compare = new Option<string[]>("--compare") { Description = "Further report.json files to add as arms", AllowMultipleArgumentsPerToken = true };
        var output = new Option<string?>("--out", "-o") { Description = "Write the Markdown metrics here" };
        var title = new Option<string?>("--title") { Description = "Heading for the metrics table" };

        var cmd = new Command("eval", "Evaluate a report against labels") { report, labels, compare, output, title };
        cmd.SetAction(async (r, ct) =>
        {
            try
            {
                var reports = new List<(string, ScanReport)> { ("report", JsonReportWriter.Read(r.GetValue(report)!)) };
                foreach (var extra in r.GetValue(compare) ?? [])
                    reports.Add((Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(extra))) ?? extra, JsonReportWriter.Read(extra)));
                var result = Evaluator.Evaluate(LabelSet.Load(r.GetValue(labels)!), reports, r.GetValue(title));
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
