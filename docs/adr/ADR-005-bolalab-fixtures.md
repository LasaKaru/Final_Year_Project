# ADR-005: Purpose-built fixture API BolaLab with 16 labelled cases
Date: 2026-11-19 · Status: Accepted
## Context
Historical .NET BOLA CVEs with available code are scarce; the symbolic rules need deterministic regression tests and the IPD needs a predictable demo.
## Decision
`fixtures/BolaLab` (own solution, not part of `BolaLens.sln`, inheritance of build props stopped) contains one endpoint per case TC01–TC16: TC01–TC12 from the October workbook, TC13–TC16 for cross-layer DI, bulk delete and a perturbation.
## Consequences
+ Golden tests in CI; explainable demo. − BolaLab is the *development* set: its numbers are preliminary and must be reported separately from datasets A/B/C.
