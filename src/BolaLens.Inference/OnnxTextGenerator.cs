using System.Text;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace BolaLens.Inference;

/// <summary>Runs a local ONNX small language model (ONNX Runtime GenAI) over a prompt and returns the
/// raw text. Greedy decoding for repeatability; stops early when the first JSON object is complete.
/// Input: model folder (genai_config.json + *.onnx). Limitation: CPU build; one generation at a time.</summary>
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

    public (string Text, int PromptTokens, int OutputTokens) Generate(string prompt, int maxLength, CancellationToken ct)
    {
        using var sequences = _tokenizer.Encode(prompt);
        using var gp = new GeneratorParams(_model);
        gp.SetSearchOption("max_length", maxLength);
        gp.SetSearchOption("do_sample", false);
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
        return (sb.ToString(), sequences[0].Length, outTokens);
    }

    public void Dispose()
    {
        _tokenizer.Dispose();
        _model.Dispose();
        _oga.Dispose();
    }
}
