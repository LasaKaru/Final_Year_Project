# D — Overall system architecture and design (IPD section D)

BolaLens finds **Broken Object Level Authorization (BOLA, OWASP API1:2023)** in ASP.NET Core Web APIs *statically*: it follows a
client-supplied object id from an endpoint, through services and repositories, to the Entity Framework Core operation that reads
or changes that object, and asks one question — **is there a check, on the same object, that binds it to the calling user before the
change happens?** A local small language model (SLM) can then review the evidence for the uncertain cases.

## 1. Architecture style

*Pipes and filters* inside a *layered* .NET solution (9 projects). Only `Cli` and `Web` are composition roots; both use the same
`App.ScanService`, so the command line, the web UI and the GitHub Action produce identical reports.

| Diagram | What it shows |
|---|---|
| [`components.png`](../diagrams/components.png) | Component diagram: front ends → App → the three stages → Core |
| [`pipeline.png`](../diagrams/pipeline.png) | Activity / data-flow diagram of one scan (input solution → report → metrics) |
| [`sequence-scan.png`](../diagrams/sequence-scan.png) | Sequence diagram of `bolalens scan --mode hybrid` |
| [`algorithm.png`](../diagrams/algorithm.png) | Decision flow of the core algorithm: id taint → sink → guard → verdict (rules SYM-01 … SYM-11) |
| [`fusion.png`](../diagrams/fusion.png) | AI/ML pipeline of the semantic stage: candidate → slice → SLM → gate → conservative fusion |
| [`class-core.png`](../diagrams/class-core.png) | Class diagram of the core model and the validator abstraction |
| [`data-model.png`](../diagrams/data-model.png) | Data model of the report and of the inferred ownership schema |
| [`evaluation-pipeline.png`](../diagrams/evaluation-pipeline.png) | Experimental pipeline: datasets A/B/C, recorded runs, labels, CodeQL baseline, metrics |
| [`../screenshots/`](../screenshots) | UI design as built: dashboard, finding detail, call graph (desktop, phone, dark mode) |

## 2. Components

| Project | Stage | Responsibility | Key classes |
|---|---|---|---|
| `BolaLens.Core` | — | Records (endpoints, paths, sinks, guards, verdicts, report), configuration, JSON options; no Roslyn | `ScanReport`, `Finding`, `ExecutionPath`, `BolaLensConfig` |
| `BolaLens.Analysis` | 1 | Roslyn engine: load the solution, endpoints, policy map, ownership schema, DI, taint walker, guards, rules | `WorkspaceLoader`, `EndpointEnumerator`, `DbContextSchemaMapper`, `PathWalker`, `GuardDetector`, `Dominance`, `SymbolicClassifier` |
| `BolaLens.Slicing` | 2a | Label-free IR slices, prompt v1, few-shot examples and gate probes from `fixtures/Calibration` | `IrSliceGenerator`, `PromptBuilder` |
| `BolaLens.Inference` | 2b | ONNX Runtime GenAI validator, JSON-schema guidance, parser, grounding, fusion | `OnnxSemanticValidator`, `OnnxTextGenerator`, `SemanticResponseParser`, `FusionPolicy` |
| `BolaLens.Reporting` | 3 | JSON, console, SARIF 2.1.0, HTML UI assets, offline HTML export | `JsonReportWriter`, `SarifReportWriter` |
| `BolaLens.Eval` | 3 | Labels, unit matching, confusion matrix, metrics, baseline SARIF arm | `Evaluator`, `UnitMatcher`, `ConfusionMatrix`, `BaselineSarif` |
| `BolaLens.App` | — | `ScanService` (shared by CLI and Web), calibration gate, `RunStore` | `ScanService`, `RunStore` |
| `BolaLens.Cli` | — | `bolalens` commands: scan, eval, robustness, endpoints, policy, schema, di, paths, slice, sarif, html | `Program` |
| `BolaLens.Web` | — | Local dashboard on `127.0.0.1:5057` (no external assets) | minimal API + static UI |

## 3. Technology choices and justification

| Choice | Why | Alternative rejected |
|---|---|---|
| C# / .NET 8 (`net8.0`), ADR-001 | Same platform as the analysed APIs and the PP; Roslyn and EF Core are first-class | .NET 10 (one-line switch later); Java/Python tools cannot use Roslyn semantics |
| Roslyn 4.14 + `MSBuildWorkspace` | Full semantic model: symbols, types, DI registrations, inherited routes; C# 14 parsed with `LanguageVersion.Preview` | Regex/AST-only parsing (no types → cannot follow interfaces); CodeQL (closed query language, generic — 0/33 in our baseline) |
| Own inter-procedural taint walker with **entity tokens** (ADR-008) | BOLA needs "same object" reasoning, not only data flow; tokens tag the loaded row so a check on `order.UserId` is matched to the change of `order` | Generic taint frameworks that know nothing about EF rows and owners |
| Four-state verdict + rule IDs (ADR-003) | Explainable, auditable; Unknown keeps honesty when evidence is missing | Binary vulnerable/safe |
| ONNX Runtime GenAI 0.20, int4 CPU models (SmolLM2-135M committed, Phi-3-mini 3.8B) | Runs offline on a 16 GB laptop; deterministic greedy decoding; source code never leaves the machine | Cloud LLM APIs (compliance stakeholder, cost, non-determinism) |
| Conservative fusion (ADR-012) | The SLM may only *suppress* a symbolic Vulnerable with a confident, grounded answer | Letting the model escalate — it only added errors on BolaLab |
| Local web UI, no framework/CDN (ADR-010) | Offline rule; reviewers need the path as a call graph | React dashboard (build chain + network) |
| xUnit v3 + Shouldly, in-memory compilations | Tests-first for every analyser pattern (197 tests) | Tests against real repositories only (slow, not deterministic) |
| SARIF 2.1.0 + composite GitHub Action | Findings appear in GitHub code scanning; CI gate by exit code | Custom report format only |

## 4. Core algorithm (stage 1)

1. **Load** the solution with `MSBuildWorkspace`; collect compile diagnostics (scan continues on partial failure).
2. **Context extraction:** endpoints (MVC controllers and Minimal APIs, inherited `[Route]`/`[Authorize]`), the global policy map,
   the EF Core ownership schema (owner candidates such as `UserId`/`OwnerId` by name or FK to the user type, query filters, and
   membership paths such as Library → Users), and the DI registration map.
3. **Taint seeding:** every id-like, client-controlled parameter (route, query, body, nested `dto.Tag.Id`, lists `Ids`) gets a token.
4. **Inter-procedural walk** (depth ≤ 5): calls are resolved through DI and MediatR; a query such as `db.Orders.Find(id)` or
   `FirstOrDefault(o => o.Id == id)` turns the id token into an **entity token** for that row; later `Update/Remove/SaveChanges`
   on the token are **sinks** (`EfOperationClassifier`).
5. **Guard detection:** owner equality (`order.UserId != callerId → Forbid`), LINQ owner predicates, query filters, resource-based
   `AuthorizeAsync`, access-check helpers and permission services, custom access-filter attributes, owner-or-admin disjunctions.
   A guard is **valid** only if it (a) is about the *same* entity token, (b) compares with the *caller identity*
   (`ClaimsPrincipal`, current-user services — never a request value), (c) is not permissive (a helper that can return `true`
   without comparing is rejected), and (d) **dominates** the change (ADR-016: it runs on every path to it).
6. **Classification** (`SymbolicClassifier`, first match wins):

| Rule | Condition | Verdict |
|---|---|---|
| SYM-10 | endpoint restricted to configured privileged roles (e.g. Administrators) | OutOfScope |
| SYM-01 | no state change, id never lost in unresolved code | OutOfScope |
| SYM-02 | no state change found, but the id entered unresolved code | Unknown |
| SYM-03 | valid owner equality / LINQ owner predicate | Protected |
| SYM-04 | applicable query filter of the required owner level | Protected |
| SYM-05 | resource-based authorization / access check / access filter (delegated) | Protected |
| SYM-06 | `IgnoreQueryFilters` and no valid guard | Vulnerable |
| SYM-07 | detached or bulk write without owner predicate | Vulnerable |
| SYM-11 | membership-scoped object (library, organisation) without an access check | Vulnerable |
| SYM-08 | owned entity selected by a client id reaches a change with no valid guard | Vulnerable |
| SYM-09 | entity has no owner or membership relation | Unknown |

Worst verdict per endpoint: Vulnerable > Unknown > Protected > OutOfScope (`VerdictOrdering`). Read mode (ADR-011) applies the same
rules to loads that are returned to the client.

## 5. Semantic stage (stage 2 — AI/ML pipeline)

* **Candidates only** (ADR-004): Vulnerable, Unknown, and Protected-by-delegated-check (SYM-05). The same candidate set is recorded for
  both arms, so one hybrid run gives the ablation (RQ2).
* **IR slice:** the traced statements only, comments stripped, renamed line IDs, **no verdicts or rule IDs** (no label leakage, ADR-006).
* **Prompt v1** with few-shot examples written for `fixtures/Calibration` only; JSON-schema-guided greedy decoding (ADR-007);
  answer = `{status, confidence, reason, evidence line IDs}`; parse + ground (evidence IDs must exist in the slice); one retry.
* **Calibration gate** (ADR-009): 4 probe slices with known answers; a model that fails is *advisory only*
  (SmolLM2 2/4 → advisory; Phi-3-mini 3/4 → active).
* **Conservative fusion** (ADR-012): final = Protected only when symbolic = Vulnerable, model = Protected, parsed, grounded and
  confidence ≥ τ (0.80); everything else keeps the symbolic verdict. τ will be calibrated on a separate split after the label freeze.

## 6. Evaluation calculations

For labelled units (Unknown predictions are *abstentions*, kept separate):

* Precision = TP / (TP + FP) · Recall = TP / (TP + FN) · **Operational recall** = TP / (all labelled-vulnerable units, abstentions
  and missing predictions counted as misses) · FPR = FP / (FP + TN) · F1 = 2PR / (P + R).
* **Pair discrimination** (datasets A/C): a vulnerable → fixed pair counts only if the unit is flagged before the fix *and* passes
  after it.
* **Robustness stability** = unchanged units / units, per perturbation (P1 rename locals, P2 misleading comments, P3 rename private
  methods, P4 rename owner properties).
* Undefined denominators are printed as `n/a`, never 0.

## 7. Data organisation (no database — by design)

BolaLens is a static analyser: it never runs the analysed APIs, so their databases are never opened. Its own data are files:

| Data | Where | Format |
|---|---|---|
| Configuration | `bolalens.json` (+ per-target configs in `eval/datasets/configs`) | JSON, hashed into every report |
| Scan output | `report.json` (+ `.sarif`, `.html`) | `ScanReport` model — see [`data-model.png`](../diagrams/data-model.png) |
| UI run history | `.bolalens/runs/<id>/` (`RunStore`, id validated by regex) | one folder per run |
| Recorded experiments | `eval/runs/<date>-<mode>-<case>/` | `report.json.gz`, `console.txt`, `env.md`, `metrics.md` — committed |
| Labels | `fixtures/BolaLab/expected.json`, `eval/labels/` | JSON, never read by the analyser (no leakage) |
| Datasets | `eval/datasets/cases.json` | repository URL + pinned commit SHA per case |
| Models | `models/<name>/` | ONNX int4 + `genai_config.json` + SHA-256 manifest |

Files keep every result reproducible from the repository alone (NFR05) and keep the tool offline and dependency-free. The inferred
**ownership schema** (entities, keys, owner candidates, query filters, membership paths) is the "database design" BolaLens reasons
about — it is reconstructed from the analysed project's `DbContext` and entity classes.

## 8. Design changes since the PP (with reasons)

| Change | Reason | ADR |
|---|---|---|
| Guard *windows* → syntactic **dominance** for exit checks | A check inside `if (express) { … }` must not protect a change outside it | ADR-008 → ADR-016 |
| Entity tokens with same-row aliasing (key tags) | Real repositories load the same row twice (`Get(id)` then `Find(id)` in `Delete`) | ADR-008 |
| Access checks, permission services, access-filter attributes as delegated guards | Kavita `[ChapterAccess]`, Oqtane `IsAuthorized(User, …)`, Bitwarden `ICurrentContext` | ADR-014, ADR-015 |
| Membership-scoped objects (SYM-11) | Library/organisation objects have no per-user owner; silent Unknown would hide real BOLA (Kavita CVE) | ADR-015 |
| Read BOLA as secondary scope | Most real advisories are reads | ADR-011 |
| Calibration gate + conservative fusion | Tiny model always answers "vulnerable"; escalation only added FPs | ADR-009, ADR-012 |
| Local web UI instead of React | Offline rule, no build chain | ADR-010 |
| `preferredImplementations` per target | Bitwarden has a Dapper *and* an EF implementation of every repository | config |
