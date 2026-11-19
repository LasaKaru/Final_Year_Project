using BolaLens.Core.Abstractions;
using BolaLens.Core.Model;

namespace BolaLens.Inference;

/// <summary>Symbolic-only ablation arm: never invoked, so Final = Symbolic on identical candidates.</summary>
public sealed class NullSemanticValidator : ISemanticValidator
{
    public bool IsEnabled => false;
    public string ModelId => "none";
    public double? ModelLoadSeconds => null;

    public Task<SemanticVerdict> ValidateAsync(IrSlice slice, string prompt, CancellationToken ct) =>
        throw new InvalidOperationException("NullSemanticValidator is never invoked (symbolic-only mode).");

    public void Dispose() { }
}
