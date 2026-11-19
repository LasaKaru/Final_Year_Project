using System.Text.Json;

namespace BolaLens.Inference;

/// <summary>What a local model folder is: its id, family (from genai_config.json) and chat format
/// (Phi-3 markers or ChatML, detected from genai_config.json / chat_template.jinja).</summary>
public sealed record ModelInfo(string Path, string Id, string? ModelType, string ChatFormat, long SizeBytes)
{
    public static bool IsModelFolder(string dir) => File.Exists(System.IO.Path.Combine(dir, "genai_config.json"));

    public static ModelInfo Inspect(string dir, string configuredFormat = "auto")
    {
        var full = System.IO.Path.GetFullPath(dir);
        string? type = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(full, "genai_config.json")));
            if (doc.RootElement.TryGetProperty("model", out var m) && m.TryGetProperty("type", out var t)) type = t.GetString();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }

        var format = configuredFormat is "phi3" or "chatml" ? configuredFormat : Detect(full, type);
        var size = Directory.Exists(full) ? Directory.GetFiles(full).Sum(f => new FileInfo(f).Length) : 0;
        return new ModelInfo(full, System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(full)), type, format, size);
    }

    private static string Detect(string dir, string? type)
    {
        if (type?.StartsWith("phi", StringComparison.OrdinalIgnoreCase) == true) return "phi3";
        foreach (var name in new[] { "chat_template.jinja", "tokenizer_config.json" })
        {
            var f = System.IO.Path.Combine(dir, name);
            if (File.Exists(f) && File.ReadAllText(f).Contains("<|im_start|>", StringComparison.Ordinal)) return "chatml";
        }
        return type is "qwen2" or "llama" or "smollm" ? "chatml" : "phi3";
    }

    /// <summary>All model folders (containing genai_config.json) up to two levels below <paramref name="root"/>.</summary>
    public static IReadOnlyList<ModelInfo> Discover(string root)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateDirectories(root, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3 })
            .Where(IsModelFolder).Select(d => Inspect(d)).OrderBy(m => m.SizeBytes).ToList();
    }
}
