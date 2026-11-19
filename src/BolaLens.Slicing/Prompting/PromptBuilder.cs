using System.Security.Cryptography;
using System.Text;
using BolaLens.Core.Abstractions;
using BolaLens.Core.Model;

namespace BolaLens.Slicing.Prompting;

/// <summary>Fills the versioned prompt template (embedded prompt-v1.txt) with the calibration few-shot
/// examples and the slice, then wraps it in the model family's chat format (Phi-3 or ChatML).
/// Input: IrSlice. Limitation: one template for all models; very small models may need a shorter one.</summary>
public sealed class PromptBuilder
{
    private readonly string _template;
    private readonly IReadOnlyList<string> _examples;

    public const string RetrySuffix = "Return only the JSON object.";

    public PromptBuilder(string version = "v1", IReadOnlyList<string>? examples = null, string chatFormat = ChatFormat.Phi3)
    {
        Version = version;
        Format = chatFormat;
        using var s = typeof(PromptBuilder).Assembly.GetManifestResourceStream($"BolaLens.Slicing.prompt-{version}.txt")
                      ?? throw new InvalidOperationException($"Prompt template '{version}' not found");
        using var r = new StreamReader(s);
        _template = r.ReadToEnd();
        _examples = examples ?? FewShotExamples.Load();
    }

    public string Version { get; }

    /// <summary>Chat format the prompt is wrapped in ("phi3" or "chatml").</summary>
    public string Format { get; }

    /// <summary>First 16 hex of SHA-256 over template + examples (recorded for reproducibility).</summary>
    public string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Format + _template + string.Join("\n", _examples))))[..16].ToLowerInvariant();

    /// <summary>The model-independent user content (template + examples + slice).</summary>
    public string Content(IrSlice slice) =>
        _template.Replace("{{FEWSHOT}}", string.Join("\n\n", _examples), StringComparison.Ordinal)
                 .Replace("{{SLICE}}", slice.Text, StringComparison.Ordinal);

    /// <summary>The full chat-formatted prompt.</summary>
    public string Build(IrSlice slice) => ChatFormat.Wrap(Format, Content(slice));

    /// <summary>Primary prompt and the retry variant ("Return only the JSON object." inside the user turn).</summary>
    public SemanticPrompt BuildPair(IrSlice slice)
    {
        var content = Content(slice);
        return new SemanticPrompt(ChatFormat.Wrap(Format, content), ChatFormat.Wrap(Format, content.TrimEnd() + "\n" + RetrySuffix));
    }
}
