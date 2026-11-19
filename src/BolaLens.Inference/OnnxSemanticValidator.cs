using System.Diagnostics;
using BolaLens.Core.Abstractions;
using BolaLens.Core.Model;

namespace BolaLens.Inference;

/// <summary>Delegate for a text generator: (prompt, ct) → (raw text, prompt tokens, output tokens).</summary>
public delegate (string Text, int PromptTokens, int OutputTokens) TextGenerator(string prompt, CancellationToken ct);

/// <summary>Stage 2 validator backed by a local ONNX SLM: prompt → greedy (optionally schema-guided)
/// generation → parse/ground → one retry with "Return only the JSON object." → else Unknown (ADR-007).
/// The model is loaded once per scan; cold-load time is reported separately. Limitation: uncalibrated scores.</summary>
public sealed class OnnxSemanticValidator : ISemanticValidator
{
    private readonly TextGenerator _generate;
    private readonly IDisposable? _owned;
    private readonly int _retries;

    public OnnxSemanticValidator(ModelInfo model, int maxNewTokens = 160, int maxLength = 4096, int retries = 1, bool jsonGuidance = true)
    {
        var sw = Stopwatch.StartNew();
        var generator = new OnnxTextGenerator(model.Path);
        ModelLoadSeconds = sw.Elapsed.TotalSeconds;
        _owned = generator;
        var schema = jsonGuidance ? VerdictSchema.Json : null;
        _generate = (p, ct) => generator.Generate(p, maxNewTokens, maxLength, schema, ct);
        ModelId = model.Id;
        Model = model;
        _retries = retries;
    }

    /// <summary>Test seam: any text generator (e.g. a scripted fake) instead of ONNX Runtime.</summary>
    public OnnxSemanticValidator(TextGenerator generate, string modelId, int retries = 1)
    {
        _generate = generate;
        ModelId = modelId;
        _retries = retries;
    }

    public bool IsEnabled => true;
    public string ModelId { get; }
    public ModelInfo? Model { get; }
    public double? ModelLoadSeconds { get; }

    public Task<SemanticVerdict> ValidateAsync(IrSlice slice, SemanticPrompt prompt, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        string raw = "";
        ParsedResponse parsed = new(false, VerdictStatus.Unknown, null, "not run", [], "not run");
        var attempts = 0;
        for (var attempt = 0; attempt <= _retries; attempt++)
        {
            attempts++;
            try
            {
                raw = _generate(attempt == 0 ? prompt.Primary : prompt.Retry, ct).Text;
                parsed = SemanticResponseParser.Parse(raw, slice);
            }
            catch (InvalidOperationException ex)
            {
                parsed = new ParsedResponse(false, VerdictStatus.Unknown, null, ex.Message, [], ex.Message);
                break; // e.g. prompt longer than the context: retrying cannot help
            }
            if (parsed.Ok) break;
        }

        var verdict = new SemanticVerdict(
            parsed.Ok ? parsed.Status : VerdictStatus.Unknown,
            parsed.Confidence,
            parsed.Ok ? parsed.Reason : $"parse failure after {attempts} attempt(s): {parsed.Error}",
            parsed.Evidence,
            parsed.Ok,
            ModelId,
            sw.ElapsedMilliseconds,
            raw.Length > 2000 ? raw[..2000] : raw,
            attempts);
        return Task.FromResult(verdict);
    }

    public void Dispose() => _owned?.Dispose();
}
