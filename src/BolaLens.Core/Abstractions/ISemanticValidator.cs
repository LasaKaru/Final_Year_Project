using BolaLens.Core.Model;

namespace BolaLens.Core.Abstractions;

/// <summary>Stage 2: judges one IR slice. Implementations: NullSemanticValidator (symbolic-only ablation
/// arm) and OnnxSemanticValidator (local SLM). Knows nothing about Roslyn.</summary>
public interface ISemanticValidator : IDisposable
{
    /// <summary>False for the symbolic-only arm: the pipeline then never calls <see cref="ValidateAsync"/>.</summary>
    bool IsEnabled { get; }

    string ModelId { get; }

    /// <summary>Seconds spent loading the model (cold load), if any.</summary>
    double? ModelLoadSeconds { get; }

    Task<SemanticVerdict> ValidateAsync(IrSlice slice, string prompt, CancellationToken ct);
}
