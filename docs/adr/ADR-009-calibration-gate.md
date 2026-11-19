# ADR-009: A calibration gate decides whether the local model may change verdicts
Date: 2026-11-19 · Status: Accepted

## Context
The first real hybrid run used SmolLM2-135M-Instruct (int4, 86 MB). With JSON-schema guidance every answer parsed, but the
model answered `vulnerable` with confidence 0.90 for **every** slice. Fusion then escalated TC10 (correctly Unknown) to
Vulnerable and disputed TC09's delegated authorization (correctly Protected) → a new false positive. A confident but
useless model must not be able to degrade Stage 1.

## Decision
Before a hybrid scan, the model answers four labelled **probe slices** from `fixtures/Calibration` (2 vulnerable, 2 protected;
disjoint from the few-shot examples and from all evaluation data). It passes with ≥ 3/4 correct (`inference.gateMinCorrect`).
A constant answer scores 2/4 and fails. A failed model is **advisory only**: its verdicts are stored in the report
(`semantic`) but `final = symbolic`, with the reason recorded. The gate result is written to `run.modelGate`.
`scan --no-gate` disables it for experiments (to measure the ungated effect).

## Consequences
+ Hybrid mode can never be worse than symbolic-only because of a degenerate model; the gate result is evidence in itself.
+ Cheap: 4 extra generations per scan.
− Four probes are a coarse test; a model can pass the gate and still be wrong on real code. τ and the probe set will be
  revisited on the calibration split (S8).
