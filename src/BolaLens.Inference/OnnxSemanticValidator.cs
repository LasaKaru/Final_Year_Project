using System.Diagnostics;
using BolaLens.Core.Abstractions;
using BolaLens.Core.Model;

namespace BolaLens.Inference;

/// <summary>Stage 2 validator backed by a local ONNX SLM: prompt → greedy generation → parse/ground →
/// retry once with "Return only the JSON object." → else Unknown (ADR-007). The model is loaded once per
/// scan; cold-load time is reported separately. Input: slice + prompt. Limitation: uncalibrated scores.</summary>
public sealed class OnnxSemanticValidator : ISemanticValidator
{
    /// <summary>Appended to the prompt on the single retry after a parse failure.</summary>
    public const string RetrySuffix = "Return only the JSON object.";

    private readonly Func<string, int, CancellationToken, (string Text, int PromptTokens, int OutputTokens)> _generate;
    private readonly IDisposable? _owned;
    private readonly int _maxLength;
    private readonly int _retries;

    public OnnxSemanticValidator(string modelDir, string modelId, int maxLength = 3072, int retries = 1)
    {
        var sw = Stopwatch.StartNew();
        var generator = new OnnxTextGenerator(modelDir);
        ModelLoadSeconds = sw.Elapsed.TotalSeconds;
        _owned = generator;
        _generate = generator.Generate;
        ModelId = modelId;
        _maxLength = maxLength;
        _retries = retries;
    }

    /// <summary>Test seam: any text generator (e.g. a scripted fake) instead of ONNX Runtime.</summary>
    public OnnxSemanticValidator(Func<string, int, CancellationToken, (string, int, int)> generate, string modelId, int maxLength = 3072, int retries = 1)
    {
        _generate = generate;
        ModelId = modelId;
        _maxLength = maxLength;
        _retries = retries;
    }

    public bool IsEnabled => true;
    public string ModelId { get; }
    public double? ModelLoadSeconds { get; }

    public Task<SemanticVerdict> ValidateAsync(IrSlice slice, string prompt, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        string raw = "";
        ParsedResponse parsed = new(false, VerdictStatus.Unknown, null, "not run", [], "not run");
        var attempts = 0;
        for (var attempt = 0; attempt <= _retries; attempt++)
        {
            attempts++;
            var p = attempt == 0 ? prompt : prompt.TrimEnd() + "\n" + RetrySuffix;
            raw = _generate(p, _maxLength, ct).Item1;
            parsed = SemanticResponseParser.Parse(raw, slice);
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
