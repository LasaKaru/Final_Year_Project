# Q&A preparation (≈ 20 minutes, may be extended)

The examiner assesses **understanding and independent ownership**. For each question: the key points to cover and where the
proof is. These are *pointers*, not a script — answer in your own words and open the code when asked. Study list for every
class: [`class-guide.md`](class-guide.md). Older short map: [`qa-evidence-map.md`](qa-evidence-map.md).

## Problem and research

| Question | Key points | Evidence |
|---|---|---|
| What is BOLA and why is it hard to detect statically? | OWASP API1:2023; the code is syntactically fine — the *missing* check is the bug; needs the object, the caller and the owner relation | `docs/plan/01`, PP |
| What exactly is your contribution? | EF-aware, Roslyn-based path analysis for ASP.NET Core with entity tokens + guard binding, an honest Unknown, and an evaluated local-SLM second opinion; prior art (LLM4BOLA, BACScan…) is not .NET/EF-aware or not offline | ADR-008, critical evaluation |
| What are your research questions and what do the results say so far? | RQ1 recall on real CVEs (0.731, draft labels); RQ2 SLM FP reduction (none so far); RQ3 cost (time/memory); RQ4 explanation usefulness (rubric study pending) | `06-results` |
| Why horizontal, object-level only? | Vertical/function-level access is what generic tools (CodeQL) already check; BOLA is the #1 API risk and needs object reasoning | ADR-011, ADR-013 |

## Design and algorithms

| Question | Key points | Evidence |
|---|---|---|
| How does a route id become "tracked" and reach EF Core? | seeding of id-like client parameters; call resolution through DI/MediatR; query → entity token; Update/Remove/SaveChanges → sink | `PathWalker` (`EvalInvocation`, `TokenFrom`), TC13/TC14 |
| What counts as a valid owner check? | same entity token · compared with the caller identity (not a request value) · not permissive · dominates the change | `PathExtractor`, `GuardDetector`, `Dominance`, `RequestEntityPatternsTests` |
| What is an entity token and why not plain taint? | a tag for "this loaded row"; lets a check on `order.UserId` protect `Update(order)`; key-tag aliasing for two loads of the same row | ADR-008 |
| What is dominance here and why only syntactic? | a check that exits must run on every path to the change; structured C# makes a syntactic test enough; `goto`/exceptions not modelled; CFG only if needed | ADR-016, `DominanceTests` |
| What are SYM-08 and SYM-11? | SYM-08 owned entity changed with no valid guard; SYM-11 library/organisation-scoped object without an access check | `SymbolicRules`, ADR-015 |
| Why four verdicts? | Unknown = missing evidence (unresolved DI, no owner model) — never silently safe; OutOfScope = not object-level | ADR-003 |
| How does the ownership schema get inferred? | DbSets, keys, owner candidates by name/FK to the user type, query filters, membership paths (BFS ≤ 4 hops) | `DbContextSchemaMapper`, `OwnerCandidateInference` |
| Is there a database? | No — static analysis never runs the target; tool data are files (reports, runs, labels) for reproducibility; the "data model" is the inferred schema + report model | `04-architecture` §7, `data-model.png` |

## AI / model

| Question | Key points | Evidence |
|---|---|---|
| Why a *local* small model? | source code must not leave the premises (compliance stakeholder), cost, determinism (greedy) | NFR01, ADR-007 |
| How do you avoid label leakage? | slices strip comments, no verdicts or rule IDs; few-shot and gate probes only from `fixtures/Calibration`; labels outside code | ADR-004/006, P2 robustness |
| What is the calibration gate? | 4 probe slices with known answers; a model that fails is advisory only (SmolLM2 2/4) | ADR-009 |
| Why may the SLM only suppress? | on BolaLab both models added FPs when allowed to escalate; conservative fusion keeps proven findings | ADR-012, `FusionPolicy` |
| Your SLM changed nothing — is stage 2 useless? | an honest negative result on draft labels with one model; value now = rationale for Unknowns; next: Qwen2.5-Coder, τ calibration | `summary-hybrid.md` |
| How was τ = 0.80 chosen? | a default; it will be calibrated on a separate split after the label freeze — until then FR12 is partial | requirements FR12 |

## Evaluation

| Question | Key points | Evidence |
|---|---|---|
| How do you know you are not overfitting BolaLab? | BolaLab = development set only; real CVEs (A), their fixes (C), wild repos (B); rule changes are driven by real cases + unit tests | README, error analysis |
| Where do the labels come from? Are they reliable? | drafts derived from vendor fix commits (the fix is the ground truth for the pair); owner verifies; Dataset B blind, two raters, κ | `eval/labels/drafts/README.md`, worksheets |
| Why is precision 1.000 — too good? | 19 flagged units, all in vulnerable commits; small sample; FPs appear on Dataset C (2) and are explained | `summary-symbolic.md` |
| Why did CodeQL find nothing? | its authorization queries look for a missing `[Authorize]` on an action, not an owner binding of a client-selected object | `eval/baseline/codeql` |
| What does operational recall mean? | Unknowns and missing predictions counted as misses: 19/33 = 0.576 | `04-architecture` §6 |
| Threats to validity? | draft labels, small A set, name-based owner heuristic (P4 0.313), one machine, one model | critical evaluation |

## Process and ownership

| Question | Key points | Evidence |
|---|---|---|
| How did you use AI tools? | implementation assistance, every session logged; all results come from recorded runs; labels and conclusions are mine | `docs/ai-log.md` |
| Show me you understand class X | open it, read the doc comment, explain input → output → limitation, point to its tests | `class-guide.md` |
| Why .NET 8 after end of support? | PP platform; offline prototype never runs target code; one-line switch to .NET 10 | ADR-001 |
| What would you do differently? | label earlier; involve a second rater earlier; start the model comparison on the 16 GB laptop sooner | — |
| What is not done and is it achievable? | calibration, model comparison, tenant/creation-time BOLA, Windows run, labels, dissertation — scheduled with buffers | `05-schedule` |
