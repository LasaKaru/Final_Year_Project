using System.CommandLine;
using BolaLens.Cli.Scanning;
using BolaLens.Core.Config;
using BolaLens.Core.Model;
using BolaLens.Reporting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens scan &lt;path&gt; --mode symbolic|hybrid [--config] [--out] [--model]` — full pipeline →
/// report.json + console summary. Exit code 1 if any final verdict is Vulnerable (CI gating, FR15).</summary>
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
        var model = new Option<string?>("--model") { Description = "Override inference.modelPath (folder with genai_config.json)" };
        var quiet = new Option<bool>("--quiet", "-q") { Description = "Only print the endpoint table, not the explanations" };
        var noFail = new Option<bool>("--no-fail") { Description = "Always exit 0 when the scan completes (do not gate on findings)" };

        var cmd = new Command("scan", "Run the full pipeline and write a JSON report") { path, mode, output, config, model, quiet, noFail };
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
            if (r.GetValue(model) is { } m)
                cfg = new BolaLensConfig { Analysis = cfg.Analysis, Identity = cfg.Identity, Ownership = cfg.Ownership, Inference = CopyWithModel(cfg.Inference, m) };

            var scanMode = r.GetValue(mode) == "hybrid" ? ScanMode.Hybrid : ScanMode.Symbolic;
            ScanReport report;
            try
            {
                (report, _) = await new ScanService(cfg).ScanAsync(r.GetValue(path)!, scanMode, ct);
            }
            catch (Exception ex) when (ex is FileNotFoundException or NotSupportedException or InvalidOperationException or IOException or DllNotFoundException or TypeInitializationException)
            {
                Common.Error(ex.Message);
                return Common.LoadError;
            }

            if (r.GetValue(output) is { } outPath)
            {
                await JsonReportWriter.WriteFileAsync(report, outPath, ct);
                Console.Error.WriteLine($"wrote {outPath}");
            }
            new ConsoleSummaryRenderer(Common.CreateConsole()).Render(report, details: !r.GetValue(quiet));
            if (r.GetValue(noFail)) return Common.Ok;
            return report.Findings.Any(f => f.Final == VerdictStatus.Vulnerable) ? Common.VulnerableFound : Common.Ok;
        });
        return cmd;
    }

    private static InferenceOptions CopyWithModel(InferenceOptions o, string modelPath) => new()
    {
        ModelPath = modelPath, ModelId = o.ModelId, MaxLength = o.MaxLength, SuppressThreshold = o.SuppressThreshold,
        RetryOnParseFailure = o.RetryOnParseFailure, MaxSliceTokens = o.MaxSliceTokens, PromptVersion = o.PromptVersion,
    };
}
