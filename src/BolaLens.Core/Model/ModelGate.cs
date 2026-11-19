namespace BolaLens.Core.Model;

/// <summary>Outcome of the calibration gate: the model's answers on labelled probe slices before the scan.
/// A model that fails the gate is advisory only — its verdicts are recorded but never change the final verdict.</summary>
public sealed record ModelGateResult(bool Passed, int Correct, int Total, int MinCorrect, IReadOnlyList<string> Answers);
