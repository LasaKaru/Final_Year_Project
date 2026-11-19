# AI-use log

Rules: no AI-generated labels or results; AI-suggested code is reviewed, understood, tested and logged;
no non-public third-party code or personal data is pasted into external AI tools.

| Date | Tool | Purpose | What I used | How I verified / changed it |
|---|---|---|---|---|
| 2026-11-18 | Claude (chat) | Planning | Sprint plan + IPD guide pack (structure, task lists, code skeletons) | Reviewed; dates adapted; skeletons used as starting points only |
| 2026-11-19 | Claude Code (cloud session) | Implementation | Generated the initial solution: scaffold, BolaLab + Calibration fixtures, Roslyn analysis engine, slicer/prompt, ONNX validator, fusion, reporting, eval harness, CLI, tests, ADRs and docs (following guides 03–06) | `dotnet build` (0 warnings), 112 automated tests passing (1 model-dependent test skipped), BolaLab golden tests 16/16 with expected rule IDs, manual review of every rationale/slice. **To do (me):** read and re-explain every class (R6), rewrite anything I cannot defend, log changes here |
| 2026-11-19 | Claude Code (cloud session 2) | Implementation + experiments | Tiny local model provisioning (SmolLM2-135M → ONNX int4), chat-format detection, JSON-schema guidance, calibration gate (after observing the model's constant answers), BolaLens.App/Web (local responsive UI), offline HTML export, eval `--compare`, tests (135), docs, recorded runs | Tests green; UI checked with Playwright at desktop and phone sizes; hybrid runs repeated (identical outputs); offline run in a no-network namespace. **To do (me):** review App/Web/Inference code and the gate design before the IPD |
| 2026-11-19 | Claude Code (cloud session 3) | Implementation + dataset tooling | Real-repo hardening from Datasets A/B (MediatR, Minimal APIs, request-body entities, helper guards, OR-of-owner checks, permissive helpers, same-row aliasing, SYM-10), Phi-3-mini run, ADR-011..013, `scripts/run-case.sh` + `scripts/dataset-report.py`, case manifest, **draft** Dataset A labels derived mechanically from vendor fix commits, blind worksheets for Dataset B | Every analyser change has a unit test reproducing the real pattern (150 tests green, BolaLab 16/16 unchanged); runs recorded in `eval/runs`. **To do (me):** verify every draft label against the code (both commits), label Dataset B worksheets blind, then freeze |

The few-shot answers in `src/BolaLens.Slicing/Prompting/FewShot/` were written for calibration code only; they are not labels of any evaluation unit.
