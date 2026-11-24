# Speaker notes (talking points — rewrite them in your own words)

Timing: slides 1–19 ≈ 12 minutes, live demo ≈ 8 minutes from slide 20 → 20 minutes. Appendix slides A–E are Q&A backups.
Exported from `BolaLens-IPD.pptx` (the same notes are in PowerPoint's notes pane; regenerate with `src/build-deck.js`).

## 1. BolaLens (title slide)

~15 s. Introduce yourself and the project in one sentence: a static analyser that finds Broken Object Level Authorization in ASP.NET Core APIs. The four coloured chips are the four verdicts the tool gives — they come back on every slide. (Rewrite all notes in your own words.)

## 2. One missing check lets any user change anyone's data

~50 s. Do not re-teach BOLA — the PP covered it. Point at the code: it compiles and passes tests; the vulnerability is the missing owner check. Aim in one sentence. Then the three stages: stage 1 does the real work, stage 2 is a local model as a second opinion, stage 3 reports and measures. Everything runs offline — source code never leaves the machine.

## 3. Progress since the PP: a working, evaluated prototype

~35 s. The headline: this is not a plan any more — it is a working tool measured on real vulnerabilities. Read the four numbers. Then the three columns: completed, in progress, not started — the demo will show the completed parts live.

## 4. What changed since the PP — and why

~45 s. Every change is backed by an ADR in docs/adr — say which evidence forced it. Emphasise: the scope changes came from real CVEs, the SLM change came from measurement, not opinion.

## 5. Stakeholders shaped the requirements

~35 s. Inner ring operators, then the business, then the wider environment including attackers as a negative stakeholder. Right side: one concrete requirement each stakeholder caused. Full table in ipd/02-stakeholders.

## 6. Functional requirements: 20 of 21 implemented

~45 s. MoSCoW priorities and PP IDs are in ipd/03-requirements. Only FR12 is partial: the threshold exists but is not calibrated yet — calibration on labelled data comes after the freeze, on purpose, to avoid tuning on the test set.

## 7. Non-functional requirements — measured, not claimed

~25 s. Each NFR has a measurement behind it — point at NFR01 (network-less run), NFR03 (we measured and the answer was 'no reduction'), NFR08 (memory). Two are partial: hybrid runtime on whole repositories and the Windows run.

## 8. Use cases — 4 actors, 18 use cases

~25 s. Main use case UC01 Scan solution includes context extraction, tracing and classification; UC06 (local SLM) extends it in hybrid mode and is partial because τ is uncalibrated. UC18 calibration and model comparison is pending. Full UC01 description in ipd/03-requirements.

## 9. Architecture: pipes and filters in a layered .NET solution

~45 s. Walk the diagram left to right: two front ends, one App service, three stages, a Core of plain records. Justify Roslyn (types and DI resolution) and the local model (compliance). If asked about a database: the tool does not run the analysed application; its own data are JSON files so that every result is reproducible from the repository.

## 10. Core algorithm: id taint → sink → guard → verdict

~1 min. Four steps on the left, then the rule table: first match wins, worst verdict per endpoint. Stress step 4 — binding — that is where BOLA lives: the check must be about the same row, compare with the caller (never a request value), not be permissive, and run on every path to the change (ADR-016). Decision flow diagram: ipd/diagrams/algorithm.png.

## 11. Guard binding: one correct check, four ways to get it wrong

~35 s. This is BolaLab, the development fixture (16 cases, labels only in expected.json). Left: the correct check. Right: four realistic mistakes the tool must tell apart — no check, comparing with a request value, checking the wrong object, checking too late. I will show these live in the UI.

## 12. Design: core model and data organisation

~20 s. The core records and the validator abstraction (Null vs ONNX validator = the ablation switch). The sequence diagram is in Appendix E. The 'database design' of this project is the ownership schema BolaLens reconstructs from the analysed DbContext, plus the report model (ER diagram in ipd/diagrams/data-model.png).

## 13. Stage 2: a local SLM as a second opinion — with guard rails

~35 s. The model only sees candidates — Vulnerable, Unknown, or protected by a delegated check. It answers in a JSON schema with evidence line IDs that must exist in the slice. A model that fails the gate cannot change anything. And even a passing model may only suppress, never escalate (ADR-012) — because on BolaLab both models only added false positives when allowed to escalate.

## 14. UI design as built: local, offline, explainable

~15 s. Screenshots of the real UI (Playwright, desktop and phone). Each finding shows the call graph controller → service → repository → sink, the checks found and why each was rejected. No CDN or external asset — it runs on 127.0.0.1. I will show it live in the demo.

## 15. Testing: 197 automated tests, development set 16 / 16

~40 s. 197 tests: 103 analyser, 34 inference, 51 integration, 9 web. BolaLab is where the rules were developed, so 16/16 is expected — it proves correctness, not generalisation. The interesting rows are the model rows: they are why the gate and conservative fusion exist.

## 16. Real CVEs: BolaLens finds what CodeQL misses (preliminary)

~1 min. Dataset A = the vulnerable commits of published advisories, Dataset C = the vendor's fixed commits. A pair counts only if flagged before and passed after the fix: 13 of 25. CodeQL's authorization queries look for a missing [Authorize] on an action, so they find none of these. Precision 1.000 on a small set with draft labels — I will verify every label before the freeze. Misses are explained in eval/results/error-analysis.md.

## 17. RQ2: the local SLM did not reduce false positives — yet

~40 s. Research question 2 asks whether a local model reduces false positives. With Phi-3-mini on the Oqtane, Kavita and IoTSharp cases the answer so far is no: it changed nothing, and its disagreements were mostly wrong. The design worked as intended — the guard rails stopped it from adding errors. Bitwarden and Jellyfin need the 16 GB laptop (memory).

## 18. Updated schedule: engineering ahead of plan

~40 s. Red line is today. Done items finished far earlier than the baseline (AI-assisted, logged). Ethics is late — say what you are doing about it. The critical path now runs through labels and writing, not code: label freeze 21 March, then calibration, final experiments, dissertation, viva 15 June. Baseline chart: ipd/diagrams/gantt-baseline-2026-11-18.png.

## 19. Critical evaluation

~50 s. Be self-critical: precision 1.000 is on a small set with draft labels; owner inference relies partly on names; the SLM has not helped yet. Then the judgement: software on track, research risk is labels and writing — and risk R6, understanding AI-assisted code, which I mitigate with the class guide and code walkthroughs.

## 20. Live demo

~8 min (slides 1–19 take ~12 min). Switch to the terminal. Follow ipd/08-demo/demo-script.md (8 minutes). If anything hangs more than 20 seconds, say so and use the recorded run in eval/runs.

## 21. Appendix A — decision flow of the symbolic stage

Backup for questions on the rule order.

## 22. Appendix B — experimental pipeline

Backup for questions on datasets, labels and metric definitions.

## 23. Appendix C — report and ownership data model

Backup for the 'database design' question.

## 24. Appendix D — robustness (BolaLab)

Backup for robustness questions.

## 25. Appendix E — sequence of a hybrid scan

Backup: order of calls in one scan — load, symbolic pipeline per endpoint and id, then slice → validate → fuse per candidate, report, exit code.
