using System.CommandLine;
using BolaLens.Slicing;
using BolaLens.Slicing.Prompting;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens slice &lt;path&gt; --endpoint …` — prints the label-free IR slice of each path of the
/// endpoint (and the full prompt with --prompt). Used for the demo and to build calibration examples.</summary>
public static class SliceCommand
{
    public static Command Create()
    {
        var path = Common.PathArgument();
        var endpoint = Common.EndpointOption();
        var prompt = new Option<bool>("--prompt") { Description = "Print the complete SLM prompt instead of only the slice" };
        var config = Common.ConfigOption();
        var cmd = new Command("slice", "Print the IR slice (and optionally the prompt) for an endpoint") { path, endpoint, prompt, config };
        cmd.SetAction(async (r, ct) =>
        {
            var cfg = Common.LoadConfig(r.GetValue(config));
            var result = await Common.AnalyseAsync(r.GetValue(path)!, cfg, r.GetValue(endpoint), false, ct);
            if (result is null) return Common.LoadError;
            if (result.Paths.Count == 0)
            {
                Common.Error("no paths for that endpoint (check the verb/route with `bolalens endpoints`)");
                return Common.LoadError;
            }
            var slicer = new IrSliceGenerator(result.Policy, result.Schema, cfg.Inference.MaxSliceTokens);
            var builder = new PromptBuilder(cfg.Inference.PromptVersion);
            foreach (var (p, _) in result.Paths)
            {
                var slice = slicer.Generate(p);
                Console.WriteLine($"# {p.Endpoint.Id} via '{p.TaintSource.Name}'  (path {p.Id}, ~{slice.EstimatedTokens} tokens)");
                Console.WriteLine(r.GetValue(prompt) ? builder.Build(slice) : slice.Text);
                Console.WriteLine();
            }
            return Common.Ok;
        });
        return cmd;
    }
}
