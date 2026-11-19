# AI-use log

Rules: no AI-generated labels or results; AI-suggested code is reviewed, understood, tested and logged;
no non-public third-party code or personal data is pasted into external AI tools.

| Date | Tool | Purpose | What I used | How I verified / changed it |
|---|---|---|---|---|
| 2026-11-18 | Claude (chat) | Planning | Sprint plan + IPD guide pack (structure, task lists, code skeletons) | Reviewed; dates adapted; skeletons used as starting points only |
| 2026-11-19 | Claude Code (cloud session) | Implementation | Generated the initial solution: scaffold, BolaLab + Calibration fixtures, Roslyn analysis engine, slicer/prompt, ONNX validator, fusion, reporting, eval harness, CLI, tests, ADRs and docs (following guides 03–06) | `dotnet build` (0 warnings), 112 automated tests passing (1 model-dependent test skipped), BolaLab golden tests 16/16 with expected rule IDs, manual review of every rationale/slice. **To do (me):** read and re-explain every class (R6), rewrite anything I cannot defend, log changes here |

The few-shot answers in `src/BolaLens.Slicing/Prompting/FewShot/` were written for calibration code only; they are not labels of any evaluation unit.
