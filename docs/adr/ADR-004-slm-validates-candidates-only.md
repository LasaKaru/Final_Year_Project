# ADR-004: The SLM validates only symbolic candidates
Date: 2026-11-19 · Status: Accepted
## Context
RQ2 compares hybrid vs symbolic-only. If the SLM saw different units, differences could come from coverage, not reasoning.
## Decision
Invoke the SLM only on paths the symbolic stage marks Vulnerable, Unknown, or Protected-by-delegation (SYM-05) — `FusionPolicy.IsCandidate`. Same candidates, same labels in both arms; one hybrid report yields both arms for `bolalens eval`. Slices never contain the symbolic verdict or rule IDs.
## Consequences
+ Clean ablation; lower inference cost. − The SLM cannot discover paths the symbolic stage missed (recall bounded by Stage 1) — reported as a limitation.
## Alternatives considered
SLM on every endpoint (costly, confounded); SLM-only baseline (kept as an optional exploratory arm).
