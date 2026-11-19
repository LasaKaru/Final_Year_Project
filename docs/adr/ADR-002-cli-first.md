# ADR-002: CLI-first; dashboard deferred and reduced to "Could"
Date: 2026-11-19 · Status: Accepted
## Context
The IPD judges working analysis, a repository and a live demo — not UI. The PP already named CLI-first as the cheaper prototype path.
## Decision
Deliver a System.CommandLine CLI whose every intermediate artefact is inspectable as JSON (`endpoints`, `policy`, `schema`, `di`, `paths`, `slice`, `scan`, `eval`). SARIF (April) is the explainability surface in GitHub; an HTML/React view is "Could" (FR16).
## Consequences
+ Demo-able, scriptable, CI-friendly (exit codes). − Less visual for non-technical examiners; mitigated by slides and slices.
