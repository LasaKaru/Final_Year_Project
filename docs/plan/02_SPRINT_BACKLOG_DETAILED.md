# 02 — Detailed Sprint Backlog (S0 → S5, then post-IPD)

How to use this file:
- Each task has an **ID**, **estimate (h)**, **priority** (Must / Should / Could), **steps**, and **Done when** (acceptance criteria).
- Create one GitHub Issue per task (title = `ID: name`), put them on a GitHub Projects board with columns *Backlog → This sprint → In progress → Review → Done*.
- Branch name = `feature/<ID>-short-name` (e.g. `feature/S1-03-policy-mapper`). Commit messages reference the issue (`#12`).
- Code references point to `05_IMPLEMENTATION_GUIDE.md` (§ numbers) and `04_SOLUTION_ARCHITECTURE_AND_STRUCTURE.md`.

Legend: **[CODE]** coding, **[DOC]** documentation, **[DATA]** dataset work, **[ADMIN]** admin, **[LEARN]** study.

---

## Sprint 0 — Foundation (Wed 18 Nov – Sun 29 Nov) · ~30 h

**Sprint goal:** A repo with a building, tested .NET 10 solution, CI green, BolaLab v1 (TC01–TC04) and a working `inventory` command.

| ID | Task | Type | Est | Pri |
|---|---|---|---|---|
| S0-01 | Supervisor email + meeting + confirm IPD submission format | ADMIN | 1 | Must |
| S0-02 | Complete Ethics Part A (sec 2.3, Q1–26), get signature | ADMIN | 2 | Must |
| S0-03 | Install toolchain | ADMIN | 3 | Must |
| S0-04 | GitHub repo, board, labels, branch protection | ADMIN | 2 | Must |
| S0-05 | Scaffold solution + props files + CI | CODE | 4 | Must |
| S0-06 | Roslyn learning kata | LEARN | 5 | Must |
| S0-07 | BolaLab fixture v1 (entities, DbContext, auth, TC01–TC04) | CODE | 6 | Must |
| S0-08 | WorkspaceLoader + `inventory` command | CODE | 5 | Must |
| S0-09 | ADR-001..007 + AI log + weekly log started | DOC | 2 | Must |

### S0-01 Supervisor email (1 h)
Steps: use template `10` §6. Attach this plan summary (00 + 01). Ask the 4 questions.
Done when: meeting booked; IPD submission format written into `docs/ipd/submission-requirements.md`.

### S0-02 Ethics Part A (2 h)
Steps: fill §2.3 Design & Methodology (≤400 words: design science + controlled experiments on public open-source code, no human participants, no live systems, local inference); answer Q1–18 (expected **No** for all human/tissue/data items) and Q19–25 (**Yes**); start date 01/09/2026, duration 10 months (update from 6); sign; send to supervisor.
Done when: signed PDF stored in `docs/ethics/` (do **not** commit personal phone/address — keep a redacted copy in the repo, original locally).

### S0-03 Toolchain (3 h)
Follow `03` §1–§2. Done when `dotnet --info` shows SDK 10.0.x; IDE opens a C# project; `git --version`, `python --version` work.

### S0-04 GitHub repo (2 h)
Steps: private repo `bolalens`; add supervisor as collaborator (read); README stub; labels (`code`, `doc`, `data`, `bug`, `ipd`, `must`, `should`, `could`); Projects board; protect `main` (require PR + CI pass — you can self-merge).
Done when: board shows all S0 + S1 issues.

### S0-05 Scaffold solution + CI (4 h)
Follow `03` §4–§6 exactly. Projects: Core, Analysis, Slicing, Inference, Reporting, Eval, Cli + 3 test projects.
Done when: `dotnet build` and `dotnet test` pass locally and in GitHub Actions; one trivial test exists per test project.

### S0-06 Roslyn kata (5 h) — do not skip
Follow `05` §1. Five small exercises in a scratch console app (not committed to `src/`, commit to `playground/`):
1. Parse a string of C# and print all method names (syntax only).
2. Create a `CSharpCompilation`, get a `SemanticModel`, print the type of every local variable.
3. For every invocation print the resolved `IMethodSymbol` (`ContainingType.ToDisplayString()` + name).
4. Get the `IOperation` tree of a method and print it (`OperationWalker`).
5. Given an interface method, find its implementation with `SymbolFinder.FindImplementationsAsync`.
Done when: you can explain the difference between *syntax tree*, *semantic model*, *symbol*, *operation* in your own words (write 5 lines in `docs/thesis-notes.md`).

### S0-07 BolaLab fixture v1 (6 h)
Follow `06` §1–§4. Create `fixtures/BolaLab` with its **own** solution: entities (Order, Project, Note, Document, DocumentShare, Invoice), `AppDbContext` with tenant query filters, `ICurrentUser`, JWT auth setup, and actions for TC01–TC04. Add `fixtures/BolaLab/expected.json` with those 4 entries.
Done when: `dotnet build fixtures/BolaLab/BolaLab.sln` → 0 errors, 0 warnings that matter.

### S0-08 WorkspaceLoader + `inventory` (5 h)
Follow `05` §2. Implement `MsBuildBootstrap`, `WorkspaceLoader`, `LoadedSolution`, CLI command `inventory <path>` printing: projects, target frameworks, document count, compile error count, workspace diagnostics.
Done when: running on BolaLab prints its 1 project, N documents, 0 errors; running on a non-existent path prints a friendly error and exit code ≠ 0; an integration test asserts document count > 0.

### S0-09 Docs (2 h)
Create `docs/adr/ADR-001..007` (from `00` §7), `docs/ai-log.md`, `docs/weekly-log.md`.

**Sprint 0 review (Sun 29 Nov):** demo `inventory` to yourself, record a 2-min screen capture (useful later), update board, write sprint report.

---

## Sprint 1 — Context extraction (Mon 30 Nov – Sun 13 Dec) · ~38 h

**Sprint goal:** The tool knows *what endpoints exist*, *what global/endpoint auth applies*, and *which entities are owned by whom and filtered how* — exported as JSON and unit-tested.

| ID | Task | Type | Est | Pri |
|---|---|---|---|---|
| S1-01 | Core domain models (records) + JSON serialisation | CODE | 4 | Must |
| S1-02 | Endpoint enumerator (controllers, routes, params, [Authorize]) | CODE | 7 | Must |
| S1-03 | Global Policy Mapper (Program.cs) | CODE | 6 | Must |
| S1-04 | DbContext Schema Mapper (DbSets, owner candidates, HasQueryFilter) | CODE | 7 | Must |
| S1-05 | `bolalens.json` config (identity sources, owner-name patterns) | CODE | 2 | Should |
| S1-06 | BolaLab TC05–TC16 + full expected.json | CODE | 5 | Must |
| S1-07 | Unit tests for S1-02/03/04 (in-memory compilations) | CODE | 4 | Must |
| S1-08 | CLI commands `endpoints`, `policy`, `schema` | CODE | 1 | Must |
| S1-09 | [P1] Dataset search log started; GHSA/NVD screening | DATA | 3 | Must |
| S1-10 | [P2] Stakeholder onion + table draft | DOC | 2 | Must |

### S1-01 Core models (4 h)
Implement records from `04` §5 in `BolaLens.Core`: `SourceRef`, `EndpointInfo`, `ParameterInfo`, `AuthorizeInfo`, `PolicyMap`, `EntityModel`, `OwnerCandidate`, `QueryFilterInfo`, `OwnershipSchema`. Add `JsonDefaults` (camelCase, enums as strings, indented).
Done when: round-trip serialisation test passes for each record.

### S1-02 Endpoint enumerator (7 h)
Algorithm: `05` §3. Handle: class inherits `ControllerBase`; `[Route]` on class with `[controller]` token; `[HttpGet/Post/Put/Patch/Delete("template")]`; parameter binding source (route if name appears in template; `[FromRoute]`, `[FromBody]`, `[FromQuery]`; complex type → body); `[Authorize]` (roles/policy), `[AllowAnonymous]`; ID-like parameter detection (`id`, `*Id`, `Guid`/`int`/`long` in route; DTO with `Id` property).
Done when: on BolaLab every action appears once with correct method + route + id parameters + auth attributes; test covers `[controller]` replacement and `{id:int}` constraints.

### S1-03 Policy Mapper (6 h)
Algorithm: `05` §4. Detect `AddAuthentication`/`AddJwtBearer`, `AddAuthorization(o => { o.FallbackPolicy = …; o.AddPolicy(…) })`, `AddAuthorizationBuilder().AddPolicy(…).SetFallbackPolicy(…)`, `MapControllers().RequireAuthorization()`, global `AuthorizeFilter`. Unknown constructs → `Unresolved` list with source refs.
Done when: BolaLab `policy.json` lists the JWT scheme, `DocumentEdit` policy and fallback policy correctly.

### S1-04 Schema Mapper (7 h)
Algorithm: `05` §5. Find `DbContext` subclasses, `DbSet<T>` properties, `OnModelCreating` `HasQueryFilter` lambdas (+ `IEntityTypeConfiguration<T>` → Should), referenced properties in the filter, owner candidates by name pattern **and** by FK/navigation to user type; classify owner kind User vs Tenant.
Done when: Order → owner `UserId`(User), `TenantId`(Tenant); Project → filter on `TenantId`; Note → filter on `TenantId`, owner `OwnerUserId`(User); Document → owner `OwnerUserId`, shares relation noted.

### S1-05 Config (2 h)
`bolalens.json` schema in `04` §7. Defaults compiled in; file overrides.

### S1-06 BolaLab TC05–TC16 (5 h)
From `06` §5. Complete `expected.json` (16 entries).
Done when: builds; each TC has exactly one action; expected.json validated by a test (16 unique units).

### S1-07 Tests (4 h)
Use `CompilationFactory` helper (`05` §12). One test class per component; at least 5 tests each.

### S1-08 CLI commands (1 h)
`bolalens endpoints|policy|schema <path> [--out file]`.

### S1-09 Dataset search (3 h)
`07` §2–§3. Create `eval/datasets/search-log.md` and `eval/datasets/registry.csv`. Screen ≥ 30 advisories/repos, record decision + reason.

### S1-10 Stakeholders (2 h)
`08` §1. Draft onion diagram (draw.io) + table.

**Sprint 1 review (Sun 13 Dec).** Supervisor meeting ~11 Dec: show the three JSON exports.

---

## Sprint 2 — Path extraction (Mon 14 Dec – Sun 27 Dec) · ~30 h (holiday-reduced)

**Sprint goal:** For every endpoint with an ID parameter, produce annotated paths from the ID to EF Core sinks across controller → service → repository, with guards and source spans.

| ID | Task | Type | Est | Pri |
|---|---|---|---|---|
| S2-01 | DI registration map + implementation resolver | CODE | 5 | Must |
| S2-02 | Caller-identity recogniser | CODE | 3 | Must |
| S2-03 | Intra-procedural taint walker (IOperation) | CODE | 7 | Must |
| S2-04 | Inter-procedural propagation (arg→param, depth bound, cache) | CODE | 5 | Must |
| S2-05 | EF Core sink & query classifier (incl. IgnoreQueryFilters, Attach, ExecuteUpdate/Delete) | CODE | 4 | Must |
| S2-06 | Guard detector (if-guard, LINQ owner predicate, resource authorization) | CODE | 5 | Must |
| S2-07 | `paths` CLI command + tests on TC01–TC16 | CODE | 3 | Must |
| S2-08 | [P2] Consolidated FR/NFR list (MoSCoW + PP mapping) | DOC | 2 | Must |
| S2-09 | [P1] Dataset B shortlist + build check | DATA | 3 | Should |

### S2-01 DI resolver (5 h)
`05` §6. Parse `AddScoped/AddTransient/AddSingleton<TService,TImpl>()` and `typeof` forms; fallback `SymbolFinder.FindImplementationsAsync`; 1 impl → resolved; many → use DI map else Unknown(ambiguous); 0 → Unknown(external).
Done when: TC13/TC14 controller → `IInvoiceService` → `InvoiceService` resolved; TC10 → Unknown(ambiguous).

### S2-02 Caller identity (3 h)
`05` §7. Recognise `User` (ControllerBase.User), `HttpContext.User`, `ClaimsPrincipal.FindFirst/FindFirstValue`, configured members (`ICurrentUser.UserId`, `.TenantId`), locals assigned from those.
Done when: tests mark `userId` in TC02 as trusted identity and `request.UserId` in TC03 as **not** trusted.

### S2-03 Taint walker (7 h)
`05` §8. Sources: route/body ID params. Propagate through locals, assignments, string/int conversions, DTO property reads. Mark *entity locals* loaded by a query whose predicate/key uses a tainted value ("object selected by tainted ID").

### S2-04 Inter-procedural (5 h)
`05` §8.4. Map tainted argument positions to callee parameters; recurse; depth ≤ 5 (config); memoise `(method, taintedParamMask)`; record each hop as a `PathStep` with `SourceRef`.

### S2-05 Sink classifier (4 h)
`05` §9. Classify by resolved method symbol (namespace `Microsoft.EntityFrameworkCore`): Read (`Find*`, `First*`, `Single*`, `Where…ToList`), Write (`Update`, `Remove`, `RemoveRange`, `Attach`, `Entry().State=`, property set on tracked entity + `SaveChanges*`), Bulk (`ExecuteUpdate*`, `ExecuteDelete*`), Bypass flag (`IgnoreQueryFilters`), Detached flag (entity created with `new` from request data).

### S2-06 Guard detector (5 h)
`05` §10. Three guard kinds + binding check (same entity instance, owner property, trusted identity) + "precedes sink with exit branch" heuristic.
Done when: guards found in TC02/TC14/TC16; wrong-variable (TC03) and wrong-object (TC04) recorded as *invalid guard* with reason.

### S2-07 `paths` command + tests (3 h)
Snapshot (Verify) tests of `paths.json` for BolaLab.

### S2-08 FR/NFR (2 h) — `08` §3–§4.
### S2-09 Dataset B shortlist (3 h) — `07` §4.

**Sprint 2 review (Sun 27 Dec).** If behind, cut S2-09 and the resource-authorization guard kind (keep it as Unknown).

---

## Sprint 3 — Verdicts, report, slicing, ONNX spike (Mon 28 Dec – Sun 10 Jan) · ~34 h

**Sprint goal:** `scan --mode symbolic` produces a correct JSON report for BolaLab; IR slices exist; one slice runs through a local ONNX model.

| ID | Task | Type | Est | Pri |
|---|---|---|---|---|
| S3-01 | Symbolic classifier rules SYM-01..SYM-08 | CODE | 5 | Must |
| S3-02 | Finding IDs, coverage manifest, JSON report writer, console summary | CODE | 4 | Must |
| S3-03 | Golden integration tests TC01–TC16 vs expected.json | CODE | 3 | Must |
| S3-04 | IR slice generator + source map + token estimate | CODE | 6 | Must |
| S3-05 | Download & hash model; ONNX GenAI "hello slice" spike | CODE | 5 | Must |
| S3-06 | Prompt template v1 + few-shot examples (from calibration fixtures only) | CODE | 3 | Must |
| S3-07 | First real open-source repo scan (symbolic) | DATA | 4 | Must |
| S3-08 | [P2] Use case + component + class + sequence + activity diagrams | DOC | 4 | Must |

### S3-01 Rules (5 h) — `05` §11 table. Each rule has an ID that appears in the report (explainability).
### S3-02 Report (4 h) — `04` §6 report schema; stable finding ID = SHA-256 of `method|route|sinkSymbol|sinkSpan` (first 12 hex).
### S3-03 Golden tests (3 h) — matching rule in `06` §6. Target ≥ 12/16; document each miss in `docs/known-limitations.md`.
### S3-04 IR slicer (6 h) — `05` §13. Tagged lines (`[ENDPOINT]`, `[POLICY]`, `[CALLER]`, `[TAINT_SOURCE]`, `[CALL]`, `[LOAD]`, `[SCHEMA]`, `[GUARD]`, `[SINK]`, `[UNRESOLVED]`), each with `L#` id and file:line. Strip comments and string literal content beyond 40 chars.
### S3-05 ONNX spike (5 h) — `03` §7 + `05` §14. Phi-3-mini-4k-instruct int4 CPU first. Measure cold load + one generation time.
### S3-06 Prompt v1 (3 h) — `05` §15. Few-shot examples must come from *calibration* fixtures (write 3 separate mini examples; never reuse TC01–TC16 code).
### S3-07 Real repo (4 h) — the best Dataset B candidate; record build issues, endpoints found, paths, Unknowns, runtime. Do not label yet.
### S3-08 Diagrams (4 h) — `09` §3 (Mermaid/PlantUML sources provided).

**Sprint 3 review (Sun 10 Jan)** + supervisor meeting ~8 Jan: show the symbolic report and the spike output.

---

## Sprint 4 — Hybrid & evidence (Mon 11 Jan – Wed 20 Jan) · ~28 h

**Sprint goal:** Hybrid mode works end-to-end on BolaLab; preliminary metrics; performance numbers; code freeze.

| ID | Task | Type | Est | Pri |
|---|---|---|---|---|
| S4-01 | `OnnxSemanticValidator` + JSON parse/validate/retry → Unknown | CODE | 5 | Must |
| S4-02 | Fusion policy (symbolic × semantic → final) with threshold τ | CODE | 2 | Must |
| S4-03 | `scan --mode hybrid` + per-slice timing + model hash in report | CODE | 2 | Must |
| S4-04 | Eval harness `bolalens eval` (confusion matrix, P/R/F1/FPR, coverage) | CODE | 5 | Must |
| S4-05 | Run symbolic vs hybrid on BolaLab ×3 repetitions; record time/RAM | DATA | 3 | Must |
| S4-06 | Re-run real repo (hybrid); manually label 10 endpoints (exploratory) | DATA | 4 | Should |
| S4-07 | README: build/run/demo instructions; known limitations | DOC | 2 | Must |
| S4-08 | Fill FR/NFR status from actual code; preliminary results table | DOC | 2 | Must |
| S4-09 | Code freeze: tag `v0.1-ipd`, release notes, demo dry-run | ADMIN | 3 | Must |

### S4-05 Measurement protocol
- Hardware/OS/CPU/RAM recorded in `eval/runs/env.md`.
- Cold run = first run after reboot or model not yet loaded; warm = subsequent.
- Peak RAM: Windows → Task Manager / `Get-Process` sample; Linux/macOS → `/usr/bin/time -v` (Linux) or `/usr/bin/time -l` (macOS). Also log `Process.GetCurrentProcess().PeakWorkingSet64` at end of scan.
- Note: BolaLab is the **development** set — say so; these are *preliminary* numbers, not the research result.

**Freeze rule:** after 20 Jan, only bug fixes on branch `ipd-fixes`; the demo runs from tag `v0.1-ipd`.

---

## Sprint 5 — IPD artefacts (Thu 21 Jan – Thu 28 Jan) · ~22 h

| ID | Task | Est | Pri |
|---|---|---|---|
| S5-01 | Slide deck v1 (17 slides, outline `09` §1) | 8 | Must |
| S5-02 | Export diagrams as PNG/SVG at readable size | 2 | Must |
| S5-03 | Gantt (from `01` §3) + deviations table | 1 | Must |
| S5-04 | Demo script + checklist; 3 timed rehearsals | 4 | Must |
| S5-05 | Backup demo video (6–8 min, 1080p) | 2 | Must |
| S5-06 | Q&A bank: write your answers to the 40 questions in `09` §6 | 3 | Must |
| S5-07 | Submit on 28 Jan (+ repo link, tag) and confirm receipt | 0.5 | Must |
| S5-08 | Ask supervisor for a 30-min mock IPD | 0.5 | Should |

---

## Post-IPD backlog (summary — detail it at each sprint planning)

**S6 (22 Feb – 7 Mar):** CFG-based dominance (`ControlFlowGraph`) to replace the precedes-heuristic; `IEntityTypeConfiguration<T>`; repository generic patterns; minimal APIs (Could); performance profiling.
**S7 (8 – 21 Mar):** finish admission A/B/C; independent endpoint inventory per repo; **freeze labels 21 Mar**; repository-family split → calibration vs held-out.
**S8 (22 – 31 Mar):** prompt v2 on calibration only; model comparison Phi-3-mini vs Phi-4-mini vs Qwen2.5-Coder-1.5B/3B (same slices, same hardware); choose τ; record everything.
**April (M4):** SARIF 2.1.0 output → GitHub code scanning; reusable GitHub Action; HTML report (Should) / small React view (Could); robustness perturbations (rename, misleading comments, equivalent guards); explanation rubric.
**May–June (M5):** run 3 comparators × 3 datasets; error taxonomy; per-repo results; dissertation chapters; final viva.
