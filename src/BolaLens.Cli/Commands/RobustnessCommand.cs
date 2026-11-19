using System.CommandLine;
using BolaLens.Analysis.Robustness;
using BolaLens.App;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens robustness &lt;path&gt; [--perturbation P1 …] [--out robustness.md]` — re-analyses in-memory perturbed
/// copies of the target (renamed locals/methods, misleading comments, renamed owner properties) and reports verdict stability (FR21).</summary>
public static class RobustnessCommand
{
    public static Command Create()
    {
        var path = Common.PathArgument();
        var config = Common.ConfigOption();
        var output = Common.OutOption();
        output.Description = "Write the Markdown table here";
        var which = new Option<string[]>("--perturbation", "-p")
        {
            Description = $"Perturbations to run (default: all): {string.Join(", ", Perturbations.All.Select(p => p.Id))}",
            AllowMultipleArgumentsPerToken = true,
        };
        var cmd = new Command("robustness", "Verdict stability under source perturbations (FR21)") { path, config, output, which };
        cmd.SetAction(async (r, ct) =>
        {
            try
            {
                var cfg = Common.LoadConfig(r.GetValue(config));
                var ids = r.GetValue(which);
                var selected = ids is { Length: > 0 } ? ids.Select(Perturbations.Find).ToList() : Perturbations.All.ToList();
                var progress = new Progress<string>(m => Console.Error.WriteLine($"[robustness] {m}"));
                var result = await new RobustnessService(cfg).RunAsync(r.GetValue(path)!, selected, ct, progress);
                var md = result.ToMarkdown();
                Console.WriteLine(md);
                if (r.GetValue(output) is { } outPath)
                {
                    var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    await File.WriteAllTextAsync(outPath, md, ct);
                    Console.Error.WriteLine($"wrote {outPath}");
                }
                return Common.Ok;
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException or IOException or System.Text.Json.JsonException)
            {
                Common.Error(ex.Message);
                return Common.LoadError;
            }
        });
        return cmd;
    }
}
