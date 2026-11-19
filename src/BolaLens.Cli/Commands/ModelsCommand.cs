using System.CommandLine;
using BolaLens.Inference;

namespace BolaLens.Cli.Commands;

/// <summary>`bolalens models [--dir models]` — lists local ONNX Runtime GenAI model folders with size,
/// family and detected chat format (no network access; models are provisioned with scripts/get-model-*.sh).</summary>
public static class ModelsCommand
{
    public static Command Create()
    {
        var dir = new Option<string>("--dir") { Description = "Folder to search", DefaultValueFactory = _ => "models" };
        var cmd = new Command("models", "List local ONNX models") { dir };
        cmd.SetAction(r =>
        {
            var models = ModelInfo.Discover(r.GetValue(dir)!);
            if (models.Count == 0)
            {
                Console.WriteLine($"No models under '{r.GetValue(dir)}'. See models/README.md (scripts/get-model-smollm2.sh).");
                return Common.Ok;
            }
            foreach (var m in models)
                Console.WriteLine($"{m.Id,-40} {m.SizeBytes / 1024.0 / 1024.0,7:0.0} MB  type={m.ModelType}  chat={m.ChatFormat}  {Path.GetRelativePath(Environment.CurrentDirectory, m.Path)}");
            return Common.Ok;
        });
        return cmd;
    }
}
