# Requirements status (filled from the real code; last update 2026-11-19, session 2)

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
| FR08 | Detect guards (owner equality, LINQ predicate, resource authorization, query filter) and validate binding | Must | F4 | ✅ (dominance 🔶) | `GuardDetector`, `PathExtractor.Bind`; windows not CFG |
| FR09 | Symbolic verdict Vulnerable/Protected/Unknown/OutOfScope with rule IDs | Must | F4 | ✅ | `SymbolicClassifier`, SYM-01..09 tests |
| FR10 | Label-free IR slices with source map and token estimate | Must | F5 | ✅ | `IrSliceGenerator`, `bolalens slice` |
| FR11 | Validate candidates with a local ONNX SLM; parse/ground JSON; symbolic or hybrid mode | Must | F6 | 🔶 | Works end to end with a real local model (SmolLM2-135M int4, JSON-schema guidance, 0 parse failures); calibration gate; research model (Phi-3) run pending |
| FR12 | Fuse symbolic and semantic verdicts with a configurable threshold | Should | F6 | 🔶 | `FusionPolicy` + calibration gate (ADR-009) implemented and tested; τ uncalibrated |
| FR13 | JSON report (findings, spans, reasons, coverage, run info) + console summary | Must | F7 | ✅ | `JsonReportWriter`, `ConsoleSummaryRenderer` |
| FR14 | SARIF 2.1.0 export | Should | F7 | ⬜ | April (M4) |
| FR15 | CI integration: exit codes + GitHub Actions | Should | F7 | 🔶 | exit codes ✅, CI smoke scan ✅, reusable Action ⬜ |
| FR16 | Visualise paths (responsive UI / HTML report) | Should | F7 | ✅ | `BolaLens.Web` local dashboard + offline HTML export (ADR-010) |
| FR17 | Evaluation harness (confusion matrix, P/R/F1/FPR/FDR, coverage) | Must | E1 | ✅ | `BolaLens.Eval`, `bolalens eval`, `MetricsTests` |
| FR18 | Configuration file (identity sources, owner patterns, depth, model) | Should | new | ✅ | `bolalens.json`, `BolaLensConfig` |
| FR19 | Inspect intermediate artefacts via CLI | Should | new | ✅ | `endpoints`, `policy`, `schema`, `di`, `paths`, `slice` |
| FR20 | Minimal-API endpoint support | Could | new | ⬜ | counted as unsupported only |
| FR21 | Robustness perturbation runner | Could | new | ⬜ | TC16 is the first manual perturbation |

## Non-functional requirements

| ID | Requirement | Status | Evidence / measurement |
|---|---|---|---|
| NFR01 | Offline operation | ✅ | No HTTP/socket code in `src/`; hybrid scan completed inside `unshare -n` (no network interfaces) — eval/runs/*-offline; UI has no external assets |
| NFR02 | Performance (< 30 min medium solution) | 🔶 | BolaLab: symbolic 4.2 s, hybrid (SmolLM2) ~82 s (~5 s/slice); real repo pending |
| NFR03 | False-positive reduction measured | 🔶 | Harness + first measurement: tiny model adds 1 FP without the gate, 0 with it; needs a capable model |
| NFR04 | Explainability | ✅ | Spans, rule IDs, rationales with rejected-guard reasons, grounded evidence lines |
| NFR05 | Reproducibility | ✅ | Tool version, target commit, config hash, prompt version+hash, model manifest hash in every report |
| NFR06 | Determinism | ✅ | Greedy decoding: two hybrid runs gave byte-identical model outputs for all 18 findings; stable finding IDs (tested) |
| NFR07 | Failure transparency | ✅ | Unresolved items, truncation, parse failures in coverage; Unknown never becomes safe |
| NFR08 | Resource budget (16 GB, int4 CPU) | 🔶 | Symbolic peak 209 MB; hybrid with 86 MB model ~1 GB; Phi-3 pending |
| NFR09 | Portability (Windows + Linux) | 🔶 | Linux (CI) ✅; Windows run pending |
| NFR10 | Maintainability | ✅ | Layered solution, 135 tests (unit, golden, pipeline, web API), CI |
| NFR11 | Safety of the tool | ✅ | Analysed code is only parsed, never executed; model has no tools/network |
| NFR12 | Usability of output | ✅ | Console summary; responsive web UI (desktop/phone, dark mode, keyboard focus) checked with Playwright; user feedback pending |
