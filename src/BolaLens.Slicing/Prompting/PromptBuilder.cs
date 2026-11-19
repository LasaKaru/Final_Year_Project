using System.Security.Cryptography;
using System.Text;
using BolaLens.Core.Model;

namespace BolaLens.Slicing.Prompting;

/// <summary>Fills the versioned prompt template (embedded prompt-v1.txt, Phi-3 chat format) with the
/// calibration few-shot examples and the slice. Input: IrSlice. Limitation: the chat markers are
/// Phi-3 specific; other model families need their own template version.</summary>
public sealed class PromptBuilder
{
    private readonly string _template;
    private readonly IReadOnlyList<string> _examples;

    public PromptBuilder(string version = "v1", IReadOnlyList<string>? examples = null)
    {
        Version = version;
        using var s = typeof(PromptBuilder).Assembly.GetManifestResourceStream($"BolaLens.Slicing.prompt-{version}.txt")
                      ?? throw new InvalidOperationException($"Prompt template '{version}' not found");
        using var r = new StreamReader(s);
        _template = r.ReadToEnd();
        _examples = examples ?? FewShotExamples.Load();
    }

    public string Version { get; }

    /// <summary>First 16 hex of SHA-256 over template + examples (recorded for reproducibility).</summary>
    public string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_template + string.Join("\n", _examples))))[..16].ToLowerInvariant();

    public string Build(IrSlice slice) =>
        _template.Replace("{{FEWSHOT}}", string.Join("\n\n", _examples), StringComparison.Ordinal)
                 .Replace("{{SLICE}}", slice.Text, StringComparison.Ordinal);
}
