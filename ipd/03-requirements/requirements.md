# C — Formal requirements, use cases and status (IPD section C)

Status as of **24 Nov 2026**, filled from the real code and recorded runs (source of truth: [`docs/requirements.md`](../../docs/requirements.md)).
✅ Implemented · 🔶 Partially implemented · ⬜ Pending. Priority = MoSCoW. PP = requirement ID in the Project Proposal.

**Summary:** 21 functional requirements — 20 ✅, 1 🔶 (FR12 threshold τ not yet calibrated) · 12 non-functional — 10 ✅, 2 🔶 (NFR02 hybrid
runtime on whole repositories, NFR09 Windows run).

## Functional requirements

| ID | Requirement | Pri | PP | Status | Evidence in the code |
|---|---|---|---|---|---|
| FR01 | Load .sln/.slnx/.csproj via MSBuildWorkspace; report load/compile diagnostics | Must | F1 | ✅ | `WorkspaceLoader`, `bolalens inventory` |
| FR02 | Enumerate endpoints (verb, route, parameters + binding, id-like, `[Authorize]`) | Must | F1 | ✅ | `EndpointEnumerator`, `EndpointEnumeratorTests` |
| FR03 | Global policy map (schemes, fallback, named policies, global filters, resource handlers) | Must | F2 | ✅ | `GlobalPolicyMapper`, `bolalens policy` |
| FR04 | EF Core ownership schema (DbSets, keys, owner candidates, query filters, membership paths) | Must | F3 | ✅ | `DbContextSchemaMapper`, `SchemaMapperTests` |
| FR05 | Resolve DI interface calls; ambiguous dispatch → Unresolved | Must | F4 | ✅ | `DiRegistrationMap`, `ImplementationResolver` (factory lambdas ⬜) |
| FR06 | Trace route/body ids controller → service → repository → EF Core (bounded depth) | Must | F4 | ✅ | `PathWalker`, golden tests TC13/TC14 |
| FR07 | Classify EF operations: load, write, commit, bulk, bypass, detached | Must | F4 | ✅ | `EfOperationClassifier`, TC07/TC08/TC12/TC15 |
| FR08 | Detect guards and validate their binding (same object, caller identity, dominance) | Must | F4 | ✅ | `GuardDetector`, `PathExtractor`, `Dominance` (ADR-014/015/016) |
| FR09 | Symbolic verdict Vulnerable / Protected / Unknown / OutOfScope with rule IDs | Must | F4 | ✅ | `SymbolicClassifier`, rules SYM-01 … SYM-11 |
| FR10 | Label-free IR slices with source map and token estimate | Must | F5 | ✅ | `IrSliceGenerator`, `bolalens slice` |
| FR11 | Validate candidates with a local ONNX SLM; parse/ground JSON; symbolic or hybrid mode | Must | F6 | ✅ | `OnnxSemanticValidator`; SmolLM2-135M + Phi-3-mini int4 runs |
| FR12 | Fuse symbolic and semantic verdicts with a configurable threshold | Should | F6 | 🔶 | `FusionPolicy` (conservative, ADR-012); τ = 0.80 **not yet calibrated** (after the label freeze) |
| FR13 | JSON report + console summary | Must | F7 | ✅ | `JsonReportWriter`, `ConsoleSummaryRenderer` |
| FR14 | SARIF 2.1.0 export | Should | F7 | ✅ | `SarifReportWriter`, validated against the OASIS schema |
| FR15 | CI integration: exit codes + GitHub Action | Should | F7 | ✅ | `action.yml`, self-tested in CI |
| FR16 | Visualise paths (UI / HTML report) | Should | F7 | ✅ | `BolaLens.Web` (local), offline HTML export, SVG call graph |
| FR17 | Evaluation harness (confusion matrix, P/R/F1/FPR, coverage, baseline arm) | Must | E1 | ✅ | `BolaLens.Eval`, `bolalens eval`, `scripts/dataset-report.py` |
| FR18 | Configuration file (identity sources, owner patterns, depth, model) | Should | new | ✅ | `bolalens.json`, `BolaLensConfig` |
| FR19 | Inspect intermediate artefacts via the CLI | Should | new | ✅ | `endpoints`, `policy`, `schema`, `di`, `paths`, `slice` |
| FR20 | Minimal-API endpoints | Could | new | ✅ | `MapGet/Post/…`, `MapGroup`, `RequireAuthorization` |
| FR21 | Robustness perturbation runner | Could | new | ✅ | `bolalens robustness` (P1–P4) |

## Non-functional requirements

| ID | Requirement | Measure | Status | Measured result |
|---|---|---|---|---|
| NFR01 | Offline operation | no network code in `src/`; hybrid scan with network disabled | ✅ | scan completed inside `unshare -n`; UI has no external assets |
| NFR02 | Performance | medium solution < 30 min on a 16 GB CPU laptop | 🔶 | symbolic 4 s (BolaLab) … 6.4 min (Jellyfin); hybrid Phi-3 ≈ 108 s per candidate → candidates only |
| NFR03 | False-positive reduction measured | hybrid vs symbolic on identical units | ✅ measured | Phi-3 changed **0** final verdicts on Dataset A/C (negative result, reported) |
| NFR04 | Explainability | spans, rule ID, reason, rejected-guard reasons | ✅ | every finding carries its path, guards and rationale |
| NFR05 | Reproducibility | tool version, target commit, config hash, prompt + model hash | ✅ | in every `report.json`; `env.md` per recorded run |
| NFR06 | Determinism | greedy decoding; repeated runs | ✅ | byte-identical model outputs on two runs (18 findings) |
| NFR07 | Failure transparency | unresolved calls, truncation, parse failures counted | ✅ | coverage manifest; Unknown never becomes safe |
| NFR08 | Resource budget | ≤ 16 GB, int4 CPU | ✅ | symbolic 0.4–7.4 GB; Phi-3 hybrid peak 8.9 GB |
| NFR09 | Portability | Windows + Linux | 🔶 | Linux CI ✅; Windows run pending (Jan) |
| NFR10 | Maintainability | layered solution, tests, CI | ✅ | 9 projects, **197 automated tests**, CI green |
| NFR11 | Safety of the tool | analysed code never executed; model has no tools/network | ✅ | Roslyn parses only; slices are text |
| NFR12 | Usability of output | readable by a developer with basic security knowledge | ✅ | console summary; responsive web UI checked at desktop and phone width; user feedback pending |

## Use case diagram

[`../diagrams/use-cases.png`](../diagrams/use-cases.png) — 4 actors, 18 use cases, coloured by status (UC06 partial: τ uncalibrated;
UC18 pending: calibration and model comparison after the label freeze). PlantUML source: `../diagrams/src/use-cases.puml`.

### UC01 Scan solution (main use case)
| Field | Content |
|---|---|
| Actors | Developer, CI pipeline, Researcher |
| Pre-conditions | .NET SDK installed; target solution restored; (hybrid) local model provisioned |
| Main flow | 1 user runs `bolalens scan <path> --mode symbolic\|hybrid` · 2 system loads the solution (Roslyn) · 3 extracts endpoints, policy map, ownership schema, DI map · 4 traces id → EF Core paths · 5 classifies each path with rules SYM-01…11 · 6 (hybrid) slices candidates and asks the local SLM · 7 fuses verdicts · 8 writes `report.json` + console summary (+ SARIF/HTML) · 9 exits 1 if anything is Vulnerable |
| Alternatives | 2a project fails to load → diagnostics in the report, scan continues · 6a model output invalid → retry once → symbolic verdict kept, parse failure counted · 6b model fails the calibration gate → its answers are advisory only |
| Post-conditions | `report.json` with findings, coverage manifest and run info (commit, config hash, model hash) |
| Requirements | FR01–FR13, NFR01, NFR02, NFR05, NFR07 |

### UC06 Validate with local SLM (extends UC01 in hybrid mode)
Trigger: a candidate path (Vulnerable, Unknown, or Protected by a delegated check SYM-05). Flow: label-free IR slice → prompt v1 with
few-shot examples from `fixtures/Calibration` → greedy generation with a JSON schema → parse + ground evidence line IDs → conservative
fusion. Exception: timeout or parse failure → symbolic verdict kept, counted in coverage.

### UC11 Run evaluation
Pre: labels (draft now, frozen 21 Mar 2027). Flow: load report(s) + labels → match units → confusion matrix → precision, recall,
operational recall, FPR, pair discrimination; optional baseline SARIF arm. Rule: undefined denominators print `n/a`.

## Changes to the requirements since the PP

| Change | Reason |
|---|---|
| **Unknown** outcome added (FR09, NFR07) | Missing evidence must never become "safe" (ADR-003) — needed for honest recall |
| FR07 expanded: bypass (`IgnoreQueryFilters`), detached and bulk writes | EF Core query filters do not protect every write path |
| Read BOLA added as a **secondary** scope | Most real advisories found for Datasets A/C are reads (ADR-011) |
| Privileged-role endpoints → OutOfScope (SYM-10) and membership objects → SYM-11 | Real code (Oqtane, Kavita, Bitwarden) uses roles and library/organisation membership, not only per-user owners (ADR-013, ADR-015) |
| Dashboard **Could → Should**, delivered as a local offline web UI (FR16) | Reviewers need the path as a picture; built without any CDN/external assets (ADR-010) |
| SARIF + GitHub Action delivered early (FR14/FR15) | Developer/CI stakeholders; cheap to build once the report model existed |
| FR20 Minimal APIs, FR21 robustness runner added | Real repositories use Minimal APIs; RQ robustness needs controlled perturbations |
| Fusion made **conservative** (the SLM may only suppress, never escalate) | Both local models only added errors when allowed to escalate (ADR-012) |
| Runtime stays **.NET 8** (as in the PP) | Owner decision, ADR-001; .NET 8 end of support (10 Nov 2026) recorded as a threat; one-line switch to .NET 10 |

## Traceability: objectives ↔ research questions ↔ requirements

(Research questions paraphrased — use the exact PP wording on the slide.)

| Objective | Research question | Requirements |
|---|---|---|
| O1 Symbolic, EF-aware extraction engine | RQ1 — can ownership paths be recovered and classified? | FR01–FR09, FR18–FR20, NFR07 |
| O2 Label-free IR + local SLM validation | RQ2 — does a local SLM reduce false positives? RQ3 — at what cost? | FR10–FR12, NFR01, NFR03, NFR06, NFR08 |
| O3 Reporting and visualisation | RQ4 — are the explanations useful? | FR13–FR16, NFR04, NFR12 |
| O4 Empirical benchmarking | RQ1–RQ4 | FR17, FR21, NFR02, NFR05 |
