# ADR-012: Conservative fusion — the SLM may only suppress, not add, findings (by default)
Date: 2026-11-19 · Status: Accepted (revisit on the calibration split, S8)

## Context
RQ2 asks whether a local SLM reduces symbolic **false positives**. Fusion originally also let the SLM escalate a symbolic
Unknown to Vulnerable and dispute a delegated (SYM-05) Protected. On the development set both local models used exactly these
two rules — and only these — to add errors: SmolLM2-135M (ungated) and Phi-3-mini (gate passed 3/4) each disputed TC09's
resource-handler authorization (a new FP) and escalated TC10 (labelled Unknown). Neither ever suppressed a true positive.

## Decision
`inference.allowEscalation` defaults to **false**: a confident, grounded "protected" may suppress a symbolic Vulnerable; all
other semantic answers are recorded as advice only. Escalation remains available for experiments (`allowEscalation: true`).

## Consequences
+ Hybrid can never report more findings than symbolic-only — a clean, falsifiable RQ2 (precision/FPR can only improve or stay).
− Potential recall gains from escalation are not used; the escalation arm can still be run and reported as an ablation.
