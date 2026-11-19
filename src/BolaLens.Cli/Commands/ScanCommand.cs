using System.CommandLine;
using BolaLens.App;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using BolaLens.Reporting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens scan &lt;path&gt; --mode symbolic|hybrid [--config] [--out] [--model] [--save]` — full
/// pipeline → report.json + console summary. Exit code 1 if any final verdict is Vulnerable (CI gating, FR15).</summary>
public static class ScanCommand
{
    public static Command Create()
    {
        var path = Common.PathArgument();
        var mode = new Option<string>("--mode", "-m") { Description = "symbolic | hybrid", DefaultValueFactory = _ => "symbolic" };
        mode.AcceptOnlyFromAmong("symbolic", "hybrid");
        var output = Common.OutOption();
        output.Description = "Write report JSON here";
        var config = Common.ConfigOption();
        var model = new Option<string?>("--model") { Description = "Model folder (with genai_config.json); overrides inference.modelPath" };
        var save = new Option<string?>("--save") { Description = "Also store the run (report + context) in this runs folder, e.g. .bolalens/runs (viewable in `bolalens ui`)" };
        var quiet = new Option<bool>("--quiet", "-q") { Description = "Only print the endpoint table, not the explanations" };
        var noGate = new Option<bool>("--no-gate") { Description = "Fuse the model's verdicts even if it fails the calibration gate (for experiments)" };
        var noFail = new Option<bool>("--no-fail") { Description = "Always exit 0 when the scan completes (do not gate on findings)" };

        var cmd = new Command("scan", "Run the full pipeline and write a JSON report") { path, mode, output, config, model, save, quiet, noGate, noFail };
        cmd.SetAction(async (r, ct) =>
        {
            BolaLensConfig cfg;
            try
            {
                cfg = Common.LoadConfig(r.GetValue(config));
            }
            catch (Exception ex) when (ex is FileNotFoundException or System.Text.Json.JsonException)
            {
                Common.Error(ex.Message);
                return Common.LoadError;
            }
            if (r.GetValue(model) is { } m) cfg = cfg.WithModel(m);
            if (r.GetValue(noGate)) cfg = cfg.WithGate(false);

            var scanMode = r.GetValue(mode) == "hybrid" ? ScanMode.Hybrid : ScanMode.Symbolic;
            ScanResult result;
            try
            {
                var progress = new Progress<string>(msg => Console.Error.WriteLine($"[scan] {msg}"));
                result = await new ScanService(cfg).ScanAsync(r.GetValue(path)!, scanMode, ct, r.GetValue(quiet) ? null : progress);
            }
            catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or InvalidOperationException or IOException or DllNotFoundException or TypeInitializationException)
            {
                Common.Error(ex.Message);
                return Common.LoadError;
            }

            var report = result.Report;
            if (r.GetValue(output) is { } outPath)
            {
                await JsonReportWriter.WriteFileAsync(report, outPath, ct);
                Console.Error.WriteLine($"wrote {outPath}");
            }
            if (r.GetValue(save) is { } runs)
            {
                var id = await new RunStore(runs).SaveAsync(result, ct);
                Console.Error.WriteLine($"saved run {id} in {runs}");
            }
            new ConsoleSummaryRenderer(Common.CreateConsole()).Render(report, details: !r.GetValue(quiet));
            if (r.GetValue(noFail)) return Common.Ok;
            return report.Findings.Any(f => f.Final == VerdictStatus.Vulnerable) ? Common.VulnerableFound : Common.Ok;
        });
        return cmd;
    }
}
