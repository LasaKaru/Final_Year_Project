# Requirements status (filled from the real code on 2026-11-19)

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
| FR11 | Validate candidates with a local ONNX SLM; parse/ground JSON; symbolic or hybrid mode | Must | F6 | 🔶 | Code complete; tested with a fake model; **real-model run pending** |
| FR12 | Fuse symbolic and semantic verdicts with a configurable threshold | Should | F6 | 🔶 | `FusionPolicy` implemented and tested; τ uncalibrated |
| FR13 | JSON report (findings, spans, reasons, coverage, run info) + console summary | Must | F7 | ✅ | `JsonReportWriter`, `ConsoleSummaryRenderer` |
| FR14 | SARIF 2.1.0 export | Should | F7 | ⬜ | April (M4) |
| FR15 | CI integration: exit codes + GitHub Actions | Should | F7 | 🔶 | exit codes ✅, CI smoke scan ✅, reusable Action ⬜ |
| FR16 | Visualise paths (HTML / minimal React view) | Could | F7 | ⬜ | April (M4) |
| FR17 | Evaluation harness (confusion matrix, P/R/F1/FPR/FDR, coverage) | Must | E1 | ✅ | `BolaLens.Eval`, `bolalens eval`, `MetricsTests` |
| FR18 | Configuration file (identity sources, owner patterns, depth, model) | Should | new | ✅ | `bolalens.json`, `BolaLensConfig` |
| FR19 | Inspect intermediate artefacts via CLI | Should | new | ✅ | `endpoints`, `policy`, `schema`, `di`, `paths`, `slice` |
| FR20 | Minimal-API endpoint support | Could | new | ⬜ | counted as unsupported only |
| FR21 | Robustness perturbation runner | Could | new | ⬜ | TC16 is the first manual perturbation |

## Non-functional requirements

| ID | Requirement | Status | Evidence / measurement |
|---|---|---|---|
| NFR01 | Offline operation | 🔶 | No HTTP/socket code in `src/` (grep); network-disabled hybrid run pending (needs the model) |
| NFR02 | Performance (< 30 min medium solution) | 🔶 | BolaLab symbolic: 4.2 s total, per-phase times in `run.phaseSeconds`; real repo pending |
| NFR03 | False-positive reduction measured | ⬜ | Needs the first real hybrid run; harness ready (two arms from one report) |
| NFR04 | Explainability | ✅ | Spans, rule IDs, rationales with rejected-guard reasons, grounded evidence lines |
| NFR05 | Reproducibility | ✅ | Tool version, target commit, config hash, prompt version+hash, model manifest hash in every report |
| NFR06 | Determinism | 🔶 | Greedy decoding; stable finding IDs (tested); 3-repetition protocol pending |
| NFR07 | Failure transparency | ✅ | Unresolved items, truncation, parse failures in coverage; Unknown never becomes safe |
| NFR08 | Resource budget (16 GB, int4 CPU) | 🔶 | Symbolic peak 209 MB; model measurement pending |
| NFR09 | Portability (Windows + Linux) | 🔶 | Linux (CI) ✅; Windows run pending |
| NFR10 | Maintainability | ✅ | Layered solution, 112 tests, CI |
| NFR11 | Safety of the tool | ✅ | Analysed code is only parsed, never executed; model has no tools/network |
| NFR12 | Usability of output | 🔶 | Console summary + rationale per finding; user feedback pending |
