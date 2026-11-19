namespace BolaLens.Slicing.Prompting;

/// <summary>Wraps the (model-independent) prompt content in a model family's chat markers.
/// Phi-3: &lt;|user|&gt; … &lt;|end|&gt; &lt;|assistant|&gt;; ChatML (SmolLM2, Qwen2.5): &lt;|im_start|&gt;role … &lt;|im_end|&gt;.</summary>
public static class ChatFormat
{
    public const string Phi3 = "phi3";
    public const string ChatMl = "chatml";

    public static string Wrap(string format, string userContent) => format switch
    {
        ChatMl => $"<|im_start|>system\nYou are a careful security code reviewer. Answer with JSON only.<|im_end|>\n<|im_start|>user\n{userContent.Trim()}<|im_end|>\n<|im_start|>assistant\n",
        _ => $"<|user|>\n{userContent.Trim()}\n<|end|>\n<|assistant|>\n",
    };
}
