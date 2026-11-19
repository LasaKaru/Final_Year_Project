namespace BolaLens.Slicing;

/// <summary>Cheap, tokenizer-free token estimate (~3.5 characters per token for code-like text).
/// Used only for the slice budget; the exact count comes from the model tokenizer at inference time.</summary>
public static class TokenEstimator
{
    public static int Estimate(string text) => (int)Math.Ceiling(text.Length / 3.5);
}
