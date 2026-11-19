using BolaLens.Core.Json;
using BolaLens.Core.Model;

namespace BolaLens.Eval;

/// <summary>One ground-truth unit (human-labelled, frozen). Matches fixtures/BolaLab/expected.json.</summary>
public sealed record LabelEntry(string? Tc, string Verb, string Route, string? Entity, VerdictStatus Label, string? ExpectedRule)
{
    public string UnitKey => Routes.UnitKey(Verb, Route);
}

/// <summary>A set of labels loaded from JSON (array of entries). Labels are never AI-generated.</summary>
public sealed class LabelSet(IReadOnlyList<LabelEntry> entries)
{
    public IReadOnlyList<LabelEntry> Entries { get; } = entries;

    public static LabelSet Load(string path) => new(JsonDefaults.Deserialize<List<LabelEntry>>(File.ReadAllText(path)));
}
