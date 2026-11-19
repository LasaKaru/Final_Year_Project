# Known limitations (v0.1, honest list for the IPD "critical evaluation" slide)

## Analysis (Stage 1)
1. **Path-insensitive guard windows** (ADR-008). Branches are walked in order and merged; loops are visited once. A guard
   protects a sink if it is on the same object and its window covers the sink. CFG dominance (`ControlFlowGraph`) is planned for S6.
2. **Controllers only.** Minimal APIs (`app.MapPut(...)`) are counted as `unsupportedEndpointStyles`, not analysed (FR20 "Could").
   Razor Pages handlers and gRPC are out of scope.
3. **Fields and static state are not tracked.** Identity copied into a field (e.g. `_userId = ...` in a constructor) is not
   recognised unless its type is configured in `identity.currentUserTypes`.
4. **DI resolution:** generic and `typeof` registrations only; factory lambdas, assembly scanning (Scrutor) and keyed services
   stay unresolved → Unknown. `IEnumerable<T>` selection at runtime is ambiguous by design (TC10).
5. **No method summaries cache:** callees are re-analysed per call site (depth ≤ `maxCallDepth`, recursion cut). Fine for
   BolaLab (0.06 s); may be slow on large solutions — measure on Dataset B.
6. **Existence-check guards** (`if (!await db.Orders.AnyAsync(o => o.Id == id && o.UserId == uid)) return Forbid();`) are not
   yet recognised as guards → may produce false positives.
7. **Generic repositories:** entity type parameters are substituted from the call's type arguments by position; unusual generic
   hierarchies fall back to SYM-09 (no ownership model).
8. **Owner candidates are name/navigation heuristics** (configurable). A field called `OwnerId` used as metadata is still a candidate;
   ownership defined only in business rules (e.g. via a join table) is not inferred. Labels are therefore human-audited.
9. **Query filters:** the scope is judged from the entity properties the filter references; the right-hand side
   (`currentUser.TenantId`) is recorded as text but not proven to be the caller identity.
10. **Mapping libraries:** `Map/ApplyTo/SetValues/TryUpdateModelAsync` on a tracked entity count as mutation by name; other
    library mutators are missed.
11. **Bulk writes and query filters:** SYM-07 is not raised for a bulk write when an applicable query filter exists (EF Core applies
    filters to `ExecuteUpdate/Delete`); this is a refinement of guide 05 §11.

## Semantic stage (Stage 2)
12. The only model run so far is **SmolLM2-135M** (86 MB): it parses perfectly with schema guidance but answers "vulnerable"
    for every slice, fails the calibration gate (2/4) and is advisory only. Detection-quality claims need Phi-3-mini or a
    0.5–1.5B coder model (S8). The gate has only 4 probes — a coarse test.
13. Model confidence is uncalibrated; τ = 0.80 is a placeholder until the calibration split exists (S8).
14. `run.modelSha256` is a manifest hash (config + graph + external-data sizes); full file hashes live in `models/README.md`.
15. Prompt v1 is one template for all models (wrapped as Phi-3 or ChatML); very small models may need a shorter prompt.

## Web UI
18. Local single-user tool: binds to 127.0.0.1, no authentication — must not be exposed on a network. Scan jobs are kept
    in memory (finished runs persist on disk); one scan runs at a time.

## Evaluation
16. BolaLab is the **development set**: rules were written while looking at it, so 16/16 there is not evidence of generalisation.
17. Unit = endpoint (worst path). Endpoints with several distinct sinks are not split per sink yet.
