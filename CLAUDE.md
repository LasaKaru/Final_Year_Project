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
* Decisions: `docs/adr/` (ADR-001 .NET 8 … ADR-010 web UI). Requirements status: `docs/requirements.md`.
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
**Done (build-flow phases 0–22 + extras):** solution/CI; BolaLab TC01–TC16; Core; loader + inventory; endpoints; policy map;
schema (incl. IEntityTypeConfiguration); DI resolver; identity; EF classifier; inter-procedural taint walker with entity
tokens; guard binding; rules SYM-01..09; JSON report + console; golden tests (16/16); IR slicer; prompt v1 (Phi-3 + ChatML);
ONNX validator with JSON-schema guidance; parser + grounding + retry; fusion; **calibration gate**; eval harness with
`--compare`; offline HTML export; **local responsive web UI**; tiny local model SmolLM2-135M int4 (86 MB) end to end.
**Findings so far:** symbolic 16/16 on BolaLab (dev set). SmolLM2-135M answers "vulnerable" for every slice → fails the gate
2/4 → advisory only; without the gate it adds 1 FP (TC09) and wrongly escalates TC10 (see eval/runs/*smollm2*).
**Next (in order):**
1. Phase 23: scan ≥ 1 real open-source ASP.NET Core + EF Core repo (pinned SHA), record coverage/Unknowns/runtime; exploratory labels.
2. Run Phi-3-mini (or Qwen2.5-Coder-0.5B/1.5B) on the 16 GB laptop: hybrid ×3, gate result, timings → eval/runs.
3. Existence-check guards (`AnyAsync(... && owner == uid)`), method-summary cache, CFG dominance (S6).
4. IPD artefacts (S5): slides, stakeholder onion, use case diagram (docs/diagrams/use-cases.puml), Gantt, demo script, backup video.
5. Post-IPD: SARIF + GitHub Action, minimal-API endpoints, robustness runner, datasets A/B/C, labels frozen 21 Mar.

## Environment notes (cloud sessions)
Network environment "Final_Year_Project" allows huggingface.co (+ us.aws.cdn.hf.co and xet/lfs CDNs) and the PyTorch CPU index,
so `scripts/get-model-smollm2.sh` works. .NET 8 SDK is installed from Ubuntu packages (`apt-get install dotnet-sdk-8.0`).
