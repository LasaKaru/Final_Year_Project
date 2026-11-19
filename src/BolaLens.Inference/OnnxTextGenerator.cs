using System.Text;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace BolaLens.Inference;

/// <summary>Runs a local ONNX small language model (ONNX Runtime GenAI) over a prompt and returns the
/// raw text. Greedy decoding for repeatability; optional JSON-schema guidance; stops early when the first
/// JSON object is complete. Input: model folder (genai_config.json + *.onnx). Limitation: CPU build; one generation at a time.</summary>
public sealed class OnnxTextGenerator : IDisposable
{
    private readonly OgaHandle _oga = new();
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;

    public OnnxTextGenerator(string modelDir)
    {
        if (!File.Exists(Path.Combine(modelDir, "genai_config.json")))
            throw new FileNotFoundException($"No genai_config.json in model folder '{modelDir}'. See models/README.md.");
        _model = new Model(modelDir);
        _tokenizer = new Tokenizer(_model);
    }

    /// <summary>Greedy generation of at most <paramref name="maxNewTokens"/> tokens (bounded by <paramref name="maxLength"/>
    /// prompt+output tokens). With <paramref name="jsonSchema"/> decoding is constrained to that schema.</summary>
    public (string Text, int PromptTokens, int OutputTokens) Generate(string prompt, int maxNewTokens, int maxLength, string? jsonSchema, CancellationToken ct)
    {
        using var sequences = _tokenizer.Encode(prompt);
        var promptTokens = sequences[0].Length;
        if (promptTokens >= maxLength)
            throw new InvalidOperationException($"prompt has {promptTokens} tokens, more than inference.maxLength={maxLength}");
        using var gp = new GeneratorParams(_model);
        gp.SetSearchOption("max_length", Math.Min(maxLength, promptTokens + maxNewTokens));
        gp.SetSearchOption("do_sample", false);
        if (jsonSchema is not null) gp.SetGuidance("json_schema", jsonSchema);
        using var generator = new Generator(_model, gp);
        generator.AppendTokenSequences(sequences);

        using var stream = _tokenizer.CreateStream();
        var sb = new StringBuilder();
        int outTokens = 0, depth = 0;
        bool started = false, inString = false, escape = false;
        while (!generator.IsDone())
        {
            ct.ThrowIfCancellationRequested();
            generator.GenerateNextToken();
            var seq = generator.GetSequence(0);
            var piece = stream.Decode(seq[^1]);
            outTokens++;
            sb.Append(piece);
            foreach (var ch in piece)
            {
                if (inString)
                {
                    if (escape) escape = false;
                    else if (ch == '\\') escape = true;
                    else if (ch == '"') inString = false;
                    continue;
                }
                if (ch == '"' && started) inString = true;
                else if (ch == '{') { depth++; started = true; }
                else if (ch == '}') depth--;
            }
            if (started && depth <= 0) break;
        }
        return (sb.ToString(), promptTokens, outTokens);
    }

    public void Dispose()
    {
        _tokenizer.Dispose();
        _model.Dispose();
        _oga.Dispose();
    }
}
