# Weekly log

## Week of 2026-11-16 (Sprint 0)
**Done:** S0-05 scaffold + CI; S0-07 BolaLab TC01–TC16 (ahead of plan: TC05–TC16 were S1-06); S0-08 loader + `inventory`;
also the whole Stage-1 pipeline, slicing, inference plumbing, eval and CLI (AI-assisted, see ai-log).
**Results:** `bolalens scan` on BolaLab → 16/16 units correct with the expected rule IDs (development set, preliminary).
**Problems:** `ClaimTypes.NameIdentifier` URI contains "xmlsoap.org" → was classed as a tenant claim → fixed by judging the last URI segment (unit test added).
Spectre.Console rendered nothing when stdout was redirected → explicit width.
**Decisions:** ADR-001 (.NET 8), ADR-008 (entity tokens + guard windows).
**Next week top 3:** walk through and re-explain all analysis classes; download Phi-3 and run `--mode hybrid` on BolaLab; start dataset search log (S1-09).
**Risks:** R4 model speed unknown until first hybrid run; R6 ownership of AI-assisted code.
