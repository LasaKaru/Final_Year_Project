# CLAUDE.md — BolaLens final-year project (read this first, every session)

**Owner:** Lasantha Karunarathne (IIT 20223087 | UoW W19598693) · Supervisor: Ms. Pudara Dewdini · Module 6COSC023C
**Project:** *Automated Context-Aware BOLA Detection in ASP.NET Core Web APIs* — tool name **BolaLens**.
This is a long-running project: keep working through the plan until the final dissertation/viva (June 2027).

## Key dates
| Date | Event |
|---|---|
| Wed 20 Jan 2027 | Code freeze, tag `v0.1-ipd` (afterwards only fixes on branch `ipd-fixes`) |
| **Thu 28 Jan 2027** | **IPD submission (IIT)** |
| 29 Jan – 19 Feb 2027 | Live IPD: 20 min presentation + 5–10 min live demo, then ≥ 20 min Q&A (may be extended) |
| Feb–Mar 2027 | CFG dominance, datasets A/B/C, **labels frozen 21 Mar**, calibration + model comparison |
| Apr 2027 | SARIF, GitHub Action, robustness perturbations |
| May–Jun 2027 | Experiments, error analysis, dissertation, final viva |

IPD brief: `docs/ipd/ipd-brief.md`. It needs: stakeholders (onion), FR/NFR with implemented/partial/pending status,
use case diagram, architecture + design diagrams, updated Gantt with deviations, critical evaluation, and a live demo of
real code (repo, modules, tests, preliminary results, code walkthrough). Diagrams alone fail.

## Where things are
* Plan pack (baseline, do not edit): `docs/plan/00…11` — **11_BUILD_FLOW_STEP_BY_STEP.md** is the ordered build list.
* Decisions: `docs/adr/` (ADR-001 .NET 8 … ADR-015 access checks + membership). Requirements status: `docs/requirements.md`.
* Limitations: `docs/known-limitations.md`. Reproducibility: `docs/reproducibility.md`. AI-use log: `docs/ai-log.md` (keep honest).
* Recorded runs: `eval/runs/<date>-<mode>-<target>/` (report.json, metrics.md, env.md).

## Architecture (pipeline inside a layered solution; .NET 8, `net8.0`)
`src/BolaLens.Core` (records, config, no Roslyn) ← `Analysis` (Stage 1 Roslyn engine) · `Slicing` (IR slices, prompts, few-shot,
calibration probes) · `Inference` (ONNX Runtime GenAI validator, parser, fusion, model detection) · `Reporting` (JSON, console,
HTML UI assets + offline export) · `Eval` (labels, confusion matrix, metrics) ← `App` (ScanService, RunStore — shared by both
composition roots) ← `Cli` (`bolalens` commands) and `Web` (`bolalens-ui`, localhost dashboard).
Fixtures: `fixtures/BolaLab` (TC01–TC16, labels only in `expected.json`), `fixtures/Calibration` (few-shot + gate probes only).

## Commands
```bash
dotnet build BolaLens.sln && dotnet build fixtures/BolaLab/BolaLab.sln && dotnet build fixtures/Calibration/Calibration.sln
dotnet test BolaLens.sln --filter "Category!=RequiresModel"          # model tests: BOLALENS_MODEL_PATH=models/<folder>
dotnet run --project src/BolaLens.Cli -- scan fixtures/BolaLab/BolaLab.sln --mode symbolic --out report.json
dotnet run --project src/BolaLens.Cli -- scan fixtures/BolaLab/BolaLab.sln --mode hybrid --model models/smollm2-135m-instruct-int4
dotnet run --project src/BolaLens.Cli -- eval --report a.json --compare b.json --labels fixtures/BolaLab/expected.json
dotnet run --project src/BolaLens.Web        # UI on http://127.0.0.1:5057 (runs stored in .bolalens/runs)
scripts/ensure-model.sh                      # verifies the committed 86 MB SmolLM2 model; rebuilds it if missing
scripts/get-model-smollm2.sh                 # (re)builds the SmolLM2-135M int4 model from Hugging Face (network once)
scripts/run-case.sh A-001                    # dataset case: clone at pinned SHA (../datasets), restore, scan, record
MODE=hybrid MODEL=$PWD/models/phi3-mini-4k/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4 scripts/run-case.sh A-005   # SLM on labelled units
python3 scripts/baseline-codeql-import.py <sarif-dir> --run-id <id>   # CodeQL baseline (workflow baseline-codeql.yml, edit eval/datasets/baseline-request.txt)
python3 scripts/dataset-report.py --worksheets   # summary + blind labelling worksheets
dotnet run --project src/BolaLens.Cli -- robustness fixtures/BolaLab/BolaLab.sln     # FR21 perturbations
dotnet run --project src/BolaLens.Cli -- scan <sln> --sarif out.sarif               # SARIF 2.1.0
```
Always rebuild before `--no-build` runs (a stale Release build once produced invalid recorded runs).
UI screenshots for slides: `docs/ipd/screenshots/`. Playwright is preinstalled in cloud sessions for UI checks.

## Non-negotiable rules
1. **Offline:** no network code in `src/`; UI has no CDN/external assets; models are local. The tiny SmolLM2 model is
   committed (`models/smollm2-135m-instruct-int4`, default `inference.modelPath`, checksums in `.sha256`); all other model
   folders stay git-ignored (Phi-3 is too big for GitHub's 100 MB file limit).
2. **No label leakage:** labels only in `expected.json` / `eval/labels`; slices strip comments; few-shot and gate probes come
   only from `fixtures/Calibration`; slices never contain verdicts or rule IDs (ADR-004/006).
3. **Honest Unknowns:** unresolved evidence → Unknown with a reason, never "safe" (ADR-003). Undefined metrics print `n/a`.
4. **Untrusted input:** analysed code is never executed; UI renders report strings as text nodes only; embedded JSON escapes `<`.
5. **Tests first** for analysers (in-memory compilations in `tests/BolaLens.Analysis.Tests`); golden tests TC01–TC16 must stay
   green with the expected rule IDs; every analyser class keeps its 3-line doc comment (purpose · input · limitation).
6. Conventional Commits, small commits, branch `dev`. Record AI assistance in `docs/ai-log.md`. The owner must be able to
   explain every class in the Q&A (risk R6) — keep code readable and documented.
7. BolaLab numbers are **development-set** results; never mix them with datasets A/B/C.

## Status (update at the end of every session)
**Done (build-flow phases 0–23 + extras, as of 19 Nov 2026):** solution/CI; BolaLab TC01–TC16; full symbolic engine (MVC +
Minimal APIs, MediatR dispatch, DI incl. library implementations, inter-procedural taint walker, entity tokens with same-row
aliasing, helper/flag/OR owner checks, permissive-helper and request-body rejection, swallowed throws, rules SYM-01..10; C# 14 preview parsing, inherited routes, getter identities, nested/list ids, owner-or-admin, custom access filters (ADR-014), preferred implementations);
read mode (ADR-011); IR slices; ONNX validator (SmolLM2 committed, Phi-3-mini run), calibration gate, conservative fusion
(ADR-012); eval harness (+ `--compare`, `--baseline-sarif` arm); SARIF 2.1.0 + reusable GitHub Action (self-tested in CI);
robustness runner P1–P4; local web UI with SVG call graph + offline HTML; dataset framework (`eval/datasets/cases.json`,
`scripts/run-case.sh`, `scripts/dataset-report.py`, `scripts/run-experiments.sh`), CodeQL baseline workflow (manual).
**Findings so far (draft labels, `eval/results/summary-symbolic.md`):** BolaLab 16/16 (dev set only). **14 A-cases** (Oqtane ×4,
Kavita ×3, Jellyfin, Bitwarden ×5, IoTSharp; 25 vuln→fixed pairs): Dataset A TP 19 / FN 7 / Unknown 5 — precision 1.000,
recall 0.731; Dataset C TN 20 / FP 2 (FPR 0.091); 13/25 pairs discriminated. **CodeQL baseline** (run 45130132411,
`eval/baseline/codeql`): 0/33 vulnerable units. **13 B-repos**, ~4,300 endpoints, blind worksheets (2,293 units).
Robustness P1–P3 1.000. Phi-3: gate 3/4, ~78 s/slice; hybrid on Dataset A/C labelled units (`--semantic-units`) in
`eval/runs/*-hybrid-A-*`. Misses = creation-time BOLA, tenant scoping without a container entity, reads built outside EF.
**Owner actions (cannot be delegated):** ethics Part A; verify draft labels (`eval/labels/drafts`); blind-label Dataset B
worksheets + 2nd-rater sample; read/explain every class (R6); supervisor sign-off of ADR-011..013; rate the RQ4 sheet
(`eval/rq4/rating-sheet.csv`, rubric `docs/evaluation/rq4-rubric.md`) with a second rater.
**Next (in order):**
1. Tenant scoping by value (IoTSharp TenantId without a container), creation-time BOLA (Bitwarden), path sensitivity (S6).
2. Phi-3 / Qwen2.5-Coder hybrid ×3 on the 16 GB laptop for A/B/C (`MODES="symbolic hybrid" scripts/run-experiments.sh`).
3. Custom policy handlers (Oqtane `[Authorize(Policy=…)]`), switch-branch-sensitive helpers (A-004 fixed FP).
4. IPD (S5): slides from `docs/ipd/*` (onion, Gantt + deviations, demo script, Q&A evidence map), rehearse, backup video.
5. After the freeze (21 Mar): final experiments, RQ4 rubric study, dissertation (`docs/thesis/outline-and-evidence-map.md`).

## Environment notes (cloud sessions)
Network environment "Final_Year_Project" allows huggingface.co (+ us.aws.cdn.hf.co and xet/lfs CDNs) and the PyTorch CPU index,
so `scripts/get-model-smollm2.sh` works. .NET 8 SDK is installed from Ubuntu packages (`apt-get install dotnet-sdk-8.0`).
