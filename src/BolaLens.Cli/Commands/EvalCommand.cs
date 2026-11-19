using System.CommandLine;
using BolaLens.Eval;
using BolaLens.Reporting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens eval --report r.json --labels l.json [--out metrics.md]` — confusion matrix and
/// P/R/F1/FPR/FDR/coverage. A hybrid report yields two arms from the same candidates: symbolic-only
/// (ablation) and hybrid (fused final verdict).</summary>
public static class EvalCommand
{
    public static Command Create()
    {
        var report = new Option<string>("--report", "-r") { Description = "report.json produced by `scan`", Required = true };
        var labels = new Option<string>("--labels", "-l") { Description = "Frozen labels (e.g. fixtures/BolaLab/expected.json)", Required = true };
        var output = new Option<string?>("--out", "-o") { Description = "Write the Markdown metrics here" };
        var title = new Option<string?>("--title") { Description = "Heading for the metrics table" };

        var cmd = new Command("eval", "Evaluate a report against labels") { report, labels, output, title };
        cmd.SetAction(async (r, ct) =>
        {
            try
            {
                var rep = JsonReportWriter.Read(r.GetValue(report)!);
                var set = LabelSet.Load(r.GetValue(labels)!);
                var arms = new List<(string, ConfusionMatrix, Metrics)>();
                var symbolicUnits = UnitMatcher.Match(set, UnitMatcher.Predict(rep, useSymbolic: true));
                var symbolicMatrix = ConfusionMatrix.From(symbolicUnits);
                arms.Add(("symbolic", symbolicMatrix, Metrics.From(symbolicMatrix)));
                var units = symbolicUnits;
                if (rep.Run.Mode == "hybrid")
                {
                    units = UnitMatcher.Match(set, UnitMatcher.Predict(rep));
                    var m = ConfusionMatrix.From(units);
                    arms.Add(("hybrid", m, Metrics.From(m)));
                }
                var md = MarkdownTable.Render(r.GetValue(title) ?? $"Evaluation — {rep.Run.TargetPath} ({rep.Run.Mode}, {rep.Run.StartedUtc:yyyy-MM-dd})", arms, units);
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
