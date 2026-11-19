# Project status & roadmap (19 Nov 2026) — what is done, what is left

Short answer: **the IPD-level prototype is largely built; the research (datasets A/B/C, three-way comparison,
RQ1–RQ4 results) and the dissertation are not started.** The proposal (PP deck) promises an *evaluated* tool —
the evaluation is where most of the remaining work and most of the final marks are.

## 1. PP promise vs. reality (traceability)

| PP item (slide) | Status | Evidence / what is missing |
|---|---|---|
| Stage 1 — Global Policy Mapper (F2) | ✅ | `Analysis/Policy`, tests, BolaLab oracle |
| Stage 1 — DbContext Schema Mapper (F3) | ✅ | `Analysis/Schema` (incl. IEntityTypeConfiguration) |
| Stage 1 — taint walker across DI layers + sink classification (F4) | ✅ | `Analysis/Taint`, `DI`, `Sinks`, `Guards`; TC13/TC14 cross-layer |
| Stage 2 — IR slice generator, context enrichment (F5) | ✅ | `Slicing`, label-free, source map, token budget |
| Stage 2 — few-shot local ONNX SLM, deterministic JSON (F6) | 🔶 | Works with SmolLM2-135M (schema-guided, greedy, gate). **No capable model evaluated yet** (Phi-3 / Qwen2.5-Coder) |
| Stage 3 — JSON diagnostics (F7) | ✅ | `report.json`, rule IDs, spans, coverage, run info |
| Stage 3 — "React developer dashboard / visual call graphs" (F7, RQ4) | 🔶 | Responsive local UI + offline HTML (ADR-010, not React). **Missing: graphical call-graph view** of a path |
| R4 — 100 % offline, < 30 min CI (N1, N2) | 🔶 | Offline proven (`unshare -n`); BolaLab 4 s / 82 s. **Not yet measured on a real, medium-size repo** |
| O1 / RQ1 — path recall on labelled cases | 🔶 | 16/16 on BolaLab (development set only). **Dataset A recall not measured** |
| O2 / RQ2 — SLM false-positive reduction vs symbolic-only | ⬜ | Harness + ablation ready; tiny model fails the gate. **Needs capable model + datasets** |
| O3 / RQ4 — explanation quality via triage rubric | ⬜ | **Rubric not designed, no review done** |
| O4 / RQ3 — 3 datasets, F1, FPR, time, RAM | ⬜ | **Datasets A/B/C not built** |
| Evaluation: 3 configurations (hybrid, symbolic-only, **baseline .NET SAST**) | 🔶 | Two arms work. **Baseline SAST tool not chosen/run** |
| Methodology: 6-step flow (requirements → dataset curation → Roslyn → IR+SLM → ablation → error analysis) | steps 1, 3, 4 ✅ · 2, 5, 6 ⬜ | Ablation is technically ready; error analysis needs data |
| Ethics form (Class 1) signed | ⬜ | Part A incomplete — do this first |
| IPD artefacts (onion, FR/NFR, use case, Gantt, slides, demo) | 🔶 | FR/NFR status, diagrams sources, screenshots exist; **slides, onion, demo script, video missing** |
| Dissertation | ⬜ | Not started (thesis notes only) |

## 2. Datasets A, B, C — concrete plan

Follow `docs/plan/07_DATASET_ACQUISITION_GUIDE.md`; log every search in `eval/datasets/search-log.md`, every candidate
in `eval/datasets/registry.csv`, apply `eval/datasets/admission-checklist.md`. **Never** copy third-party repos into this
repo — store URL + commit SHA and clone on demand. Labels are human-made, stored as `eval/labels/<case>.labels.json`
(same format as `fixtures/BolaLab/expected.json`), frozen before the final experiments.

| Dataset | Purpose (RQ) | Where to look | Realistic size | Labelling |
|---|---|---|---|---|
| **A — historical .NET BOLA/IDOR** | Recall (RQ1): vulnerable commit vs patched commit | GitHub Advisory DB (ecosystem NuGet; CWE-639, CWE-284, CWE-285, CWE-863, CWE-862), NVD keyword search ("IDOR", "authorization" + "ASP.NET"), security advisories of self-hosted .NET apps (e.g. Jellyfin, Kavita, nopCommerce, Umbraco, Orchard Core, Smartstore) and their fix commits | **5–15 cases** is a good outcome (scarce) | Pre-patch endpoint = Vulnerable, same endpoint after the fix = Protected (the fix commit is the ground truth) |
| **B — real open-source APIs (wild)** | Precision/FPR, coverage, runtime (RQ2, RQ3) | GitHub search: `language:C# "DbContext" "[ApiController]"` with stars/licence filters; reference apps (e.g. `gothinkster/aspnetcore-realworld-example-app`, `jasontaylordev/CleanArchitecture`, `dotnet/eShop`, `nopSolutions/nopCommerce`, `simplcommerce/SimplCommerce`) | **5–10 repos**, ~100–300 state-changing endpoints | Stratified manual audit of every state-changing endpoint with an id (3-point protocol: client id source · cross-user mutation · owner guard before sink); second-rater sample for agreement |
| **C — secure control apps** | False-positive noise (RQ2) | Apps whose ownership checks are verified: patched commits from Dataset A, BolaLab protected cases (reported separately), eShopOnWeb **only after** you verify its policy | 3–5 apps | Every flagged endpoint inspected; expected = Protected |

Expected reality check (write it in the dissertation, it is a finding): real repos often use **MediatR / CQRS
handlers** and **Minimal APIs** — today BolaLens marks MediatR dispatch as Unknown and only counts Minimal-API
endpoints. Supporting `IRequestHandler<TRequest>` resolution and `MapPut/MapDelete` endpoints is likely needed for
Dataset B coverage (see §4).

## 3. Methodology (how to describe and run it)

* **Research approach:** Design Science Research — build an artefact (BolaLens), evaluate it in controlled
  experiments, iterate. Quantitative, positivist evaluation; no human participants (Class 1 ethics).
* **Experimental design:** same units, three configurations — (1) symbolic-only, (2) hybrid (symbolic + local SLM,
  calibration gate on), (3) baseline .NET SAST tool — on datasets A, B, C; BolaLab reported separately as development set.
* **Ablation (RQ2):** one hybrid run gives both arms on identical candidates (ADR-004); report the gate result.
* **Bias control:** prompts/τ tuned only on a calibration split (repository-family split, `eval/splits/`), few-shot
  examples only from `fixtures/Calibration`, labels frozen before final runs, perturbation tests (renames, misleading
  comments) for robustness.
* **Metrics:** precision, recall, operational recall (Unknown = miss), F1, FPR, FDR, coverage; runtime and peak RAM per
  phase; 3 repetitions (determinism already shown).
* **RQ4 explanation quality:** a small rubric (correct root cause? correct lines? actionable fix?) scored on a sample of
  findings by you + a second rater (supervisor or peer), report agreement.
* **Error analysis:** classify every FP/FN (missed guard pattern, DI/MediatR unresolved, schema heuristic wrong, model error…).

## 4. Development still to do (priority order)

1. **Real-repo hardening** (driven by Dataset B): MediatR/CQRS handler resolution; Minimal-API endpoints (FR20);
   existence-check guards (`AnyAsync(... && owner == uid)`); method-summary cache for large solutions; `--project` filter.
2. **Capable local model:** provision Phi-3-mini (or Qwen2.5-Coder-0.5B/1.5B int4) on the 16 GB laptop; pass the gate;
   measure latency/RAM; compare models on the same slices (S8).
3. **Baseline SAST arm:** pick one (Security Code Scan / SonarAnalyzer.CSharp / Semgrep OSS / CodeQL C#), run on the same
   datasets, map its findings to endpoint units (`eval` adapter).
4. **Call-graph view in the UI** (PP RQ4 promise): render controller → service → repository → sink as an SVG graph.
5. **CFG dominance** for guards (replace protection windows, ADR-008); robustness perturbation runner (FR21).
6. **SARIF 2.1.0 + reusable GitHub Action** (FR14/FR15).

## 5. Revised timeline (PP timeline superseded)

| When | Milestone |
|---|---|
| Now – 29 Nov 2026 | Ethics Part A signed; supervisor meeting; read/understand the code; dataset search log started |
| Dec 2026 | Dataset B shortlist (10–15 repos) + first 2 real-repo scans; MediatR + existence-check support; Dataset A screening |
| Jan 2027 (to 20 Jan) | Capable model on laptop; first real-repo hybrid run; IPD slides, onion, use case, Gantt, demo script; **code freeze `v0.1-ipd`** |
| 28 Jan 2027 | **IPD submission**; demo + Q&A 29 Jan – 19 Feb |
| Feb 2027 | Datasets A/B/C admitted; labelling; baseline SAST arm; call-graph UI |
| Mar 2027 | **Labels frozen**; calibration split; model comparison; perturbation tests |
| Apr 2027 | Final experiments (3 configs × 3 datasets × 3 runs); error analysis; RQ4 rubric study; SARIF/Action |
| May 2027 | Dissertation chapters (implementation, evaluation, discussion) |
| Jun 2027 (confirm date) | Final submission + viva |
