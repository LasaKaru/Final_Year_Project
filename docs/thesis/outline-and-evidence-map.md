# Dissertation outline and evidence map (structure only — the text is written by the student)

| Chapter | Content to write | Evidence already in the repo |
|---|---|---|
| 1 Introduction | Problem (OWASP API1 BOLA), aim, objectives O1–O4, RQ1–RQ4, scope (horizontal, state-changing; read as secondary) | PP, ADR-011, `docs/project-status-and-roadmap.md` |
| 2 Literature review | SAST for authorization, taint analysis, LLM/SLM-assisted SAST, BOLA detection (DAST, specs), gap | `docs/thesis-notes.md`, PP literature |
| 3 Methodology | Design Science Research; datasets A/B/C and admission; labelling protocol + κ; three configurations; metrics; bias controls | `eval/datasets/*`, `eval/labels/*/README.md`, `scripts/run-experiments.sh`, roadmap §3 |
| 4 Requirements | Stakeholders (onion), FR/NFR with status, use cases | `docs/diagrams/stakeholder-onion.svg`, `docs/requirements.md`, `docs/diagrams/use-cases.puml` |
| 5 Design | Pipeline in layers, ADR-001…013, data model, verdict lattice, rules SYM-01…10 | `docs/adr`, `docs/diagrams/*.mmd`, README rules table |
| 6 Implementation | Loader, endpoints (MVC + Minimal APIs), policy, schema, DI + mediator, taint walker, guards, slicing, ONNX inference, fusion, reporting, UI, Action | `src/*`, tests per component |
| 7 Testing | Unit (in-memory compilations), golden TC01–TC16, integration, robustness, determinism, offline proof | `tests/*`, `eval/runs/*robustness*`, `*offline*`, determinism output of `run-experiments.sh` |
| 8 Evaluation | RQ1 recall (A), RQ2 FP reduction (B/C, ablation), RQ3 cost (time/RAM), RQ4 explanations (rubric), baseline comparison, error analysis | `eval/results/*`, `eval/runs/*` (final runs after the freeze) |
| 9 Discussion | Threats to validity (name heuristics, path-insensitivity, small Dataset A, single family Oqtane), ethics, disclosure | `docs/known-limitations.md`, error analysis |
| 10 Conclusion | Contributions, future work (CFG, permission services, field-level exposure) | — |
