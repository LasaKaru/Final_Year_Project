# Requirements status (filled from the real code; last update 2026-11-23, session 3 round 3)

✅ Implemented · 🔶 Partially implemented · ⬜ Pending. PP IDs mapped in the "PP" column (guide 08).

## Functional requirements

| ID | Requirement | Pri | PP | Status | Evidence |
|---|---|---|---|---|---|
| FR01 | Load .sln/.slnx/.csproj via MSBuildWorkspace; report load/compile diagnostics | Must | F1 | ✅ | `WorkspaceLoader`, `bolalens inventory`, integration tests |
| FR02 | Enumerate controller endpoints (verb, route, params + binding, id-like, [Authorize]/[AllowAnonymous]) | Must | F1 | ✅ | `EndpointEnumerator`, `EndpointEnumeratorTests` |
| FR03 | Global Policy Map (schemes, fallback, named policies, global filters, resource handlers) | Must | F2 | ✅ | `GlobalPolicyMapper`, `bolalens policy` |
| FR04 | EF Core ownership schema (DbSets, keys, owner candidates, query filters incl. IEntityTypeConfiguration) | Must | F3 | ✅ | `DbContextSchemaMapper`, `SchemaMapperTests` |
| FR05 | Resolve DI interface calls; ambiguous/external dispatch → Unresolved | Must | F4 | ✅ | `DiRegistrationMap`, `ImplementationResolver` (factories ⬜) |
| FR06 | Trace route/body IDs controller → service → repository to EF Core (bounded depth) | Must | F4 | ✅ | `PathWalker`, TC13/TC14 golden tests |
| FR07 | Classify EF ops: load, write, commit, bulk, bypass, detached | Must | F4 | ✅ | `EfOperationClassifier`, TC07/TC08/TC12/TC15 |
| FR08 | Detect guards (owner equality, LINQ predicate, resource authorization, query filter) and validate binding | Must | F4 | ✅ | `GuardDetector`, `PathExtractor.Bind`, `Dominance`; + helpers, OR/owner-or-admin, access checks, access filters, self-access, membership (ADR-014/015); syntactic dominance of exit checks (ADR-016), not full CFG |
| FR09 | Symbolic verdict Vulnerable/Protected/Unknown/OutOfScope with rule IDs | Must | F4 | ✅ | `SymbolicClassifier`, SYM-01..11 tests (SYM-10 privileged roles, SYM-11 membership) |
| FR10 | Label-free IR slices with source map and token estimate | Must | F5 | ✅ | `IrSliceGenerator`, `bolalens slice` |
| FR11 | Validate candidates with a local ONNX SLM; parse/ground JSON; symbolic or hybrid mode | Must | F6 | ✅ | SmolLM2-135M (gate 2/4 → advisory) and Phi-3-mini int4 (gate 3/4, 0 parse failures, ~78 s/slice CPU) end to end — eval/runs/*phi3mini; Phi-3 hybrid on all Dataset A/C ownership cases (eval/runs/2026-11-23-hybrid-*); Qwen2.5-Coder comparison pending (S8) |
| FR12 | Fuse symbolic and semantic verdicts with a configurable threshold | Should | F6 | 🔶 | `FusionPolicy`, calibration gate (ADR-009), conservative fusion default (ADR-012); τ uncalibrated |
| FR13 | JSON report (findings, spans, reasons, coverage, run info) + console summary | Must | F7 | ✅ | `JsonReportWriter`, `ConsoleSummaryRenderer` |
| FR14 | SARIF 2.1.0 export | Should | F7 | ✅ | `SarifReportWriter`, `bolalens sarif` / `scan --sarif`; validated against the OASIS schema; `SarifTests` |
| FR15 | CI integration: exit codes + GitHub Actions | Should | F7 | ✅ | exit codes, CI smoke scan, reusable composite Action `action.yml` (code-scanning upload, gate) self-tested in CI |
| FR16 | Visualise paths (responsive UI / HTML report) | Should | F7 | ✅ | `BolaLens.Web` local dashboard + offline HTML export (ADR-010) + SVG call graph per path (`ipd/screenshots/call-graph-*.png`) |
| FR17 | Evaluation harness (confusion matrix, P/R/F1/FPR/FDR, coverage) | Must | E1 | ✅ | `BolaLens.Eval`, `bolalens eval` (+ `--baseline-sarif` CodeQL arm), `scripts/dataset-report.py`, `scripts/run-experiments.sh`, `--semantic-units` |
| FR18 | Configuration file (identity sources, owner patterns, depth, model) | Should | new | ✅ | `bolalens.json`, `BolaLensConfig` |
| FR19 | Inspect intermediate artefacts via CLI | Should | new | ✅ | `endpoints`, `policy`, `schema`, `di`, `paths`, `slice` |
| FR20 | Minimal-API endpoint support | Could | new | ✅ | `MapGet/Post/Put/Patch/Delete`, `MapGroup` prefixes, `RequireAuthorization` (CleanArchitecture B-002) |
| FR21 | Robustness perturbation runner | Could | new | ✅ | `bolalens robustness` (P1–P4 in-memory perturbations); BolaLab and Oqtane: stability 1.000 for P1–P3, comments never reach slices — eval/runs/*robustness* |

## Non-functional requirements

| ID | Requirement | Status | Evidence / measurement |
|---|---|---|---|
| NFR01 | Offline operation | ✅ | No HTTP/socket code in `src/`; hybrid scan completed inside `unshare -n` (no network interfaces) — eval/runs/*-offline; UI has no external assets |
| NFR02 | Performance (< 30 min medium solution) | 🔶 | Symbolic: BolaLab 4.2 s; Oqtane (204 endpoints, 3 projects) 15–19 s, ~600 MB. Hybrid with Phi-3-mini ~78 s/slice on 4 CPU cores → only feasible for candidates, not whole repos |
| NFR03 | False-positive reduction measured | ✅ (measured; no reduction) | BolaLab: tiny model adds 1 FP without the gate, 0 with it. Dataset A/C (Phi-3-mini, 15 reports, 29 SLM calls): hybrid = symbolic arm — 0 FPs removed, 0 added; the model disagreed 9× (mostly "Vulnerable" on protected code) and conservative fusion kept the symbolic verdict (`eval/results/summary-hybrid.md`) |
| NFR04 | Explainability | ✅ | Spans, rule IDs, rationales with rejected-guard reasons, grounded evidence lines |
| NFR05 | Reproducibility | ✅ | Tool version, target commit, config hash, prompt version+hash, model manifest hash in every report |
| NFR06 | Determinism | ✅ | Greedy decoding: two hybrid runs gave byte-identical model outputs for all 18 findings; stable finding IDs (tested) |
| NFR07 | Failure transparency | ✅ | Unresolved items, truncation, parse failures in coverage; Unknown never becomes safe |
| NFR08 | Resource budget (16 GB, int4 CPU) | ✅ | Symbolic: 0.4–7.5 GB on real solutions (ErsatzTV largest); hybrid SmolLM2 ~1 GB; Phi-3-mini hybrid peak 8.9 GB, median 108 s per SLM call on 4 CPU cores |
| NFR09 | Portability (Windows + Linux) | 🔶 | Linux (CI) ✅; Windows run pending |
| NFR10 | Maintainability | ✅ | Layered solution, 135 tests (unit, golden, pipeline, web API), CI |
| NFR11 | Safety of the tool | ✅ | Analysed code is only parsed, never executed; model has no tools/network |
| NFR12 | Usability of output | ✅ | Console summary; responsive web UI (desktop/phone, dark mode, keyboard focus) checked with Playwright; user feedback pending |
