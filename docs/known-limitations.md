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

## Real-repository findings (Datasets A/B, 2026-11-19)
19. **Field-level exposure** (a returned object filters private fields for other users, CVE-2024-55471) is outside the model:
    expected miss (ADR-011).
20. **Custom permission services and string-dispatched helpers** (`IUserPermissions.IsAuthorized(User, site, entityName, id,
    perm)`, `switch (entityName)`) are not interpreted: such endpoints are reported Vulnerable in both the vulnerable and the
    fixed commit (A-004). Custom `[Authorize(Policy = …)]` handlers are likewise not interpreted.
21. A boolean helper counts as an owner check when its body compares a parameter with the caller identity; it is rejected
    when it can return the literal `true` (directly or via the returned local) — other permissive shapes are not detected.
22. Two single objects loaded by the same key value on one path are treated as one row (key-tag aliasing). This assumes the
    id is not reassigned between the loads.
23. Razor/Blazor components (source-generated partials) show up as compile errors in Oqtane-style solutions; API controllers
    are unaffected, but `projectsFailed` overstates real load problems.
24. Which roles are privileged (SYM-10) is configured per target; a wrong list hides findings (ADR-013).
25. Robustness (FR21): P4 (renamed owner properties) lowers verdict stability to 0.313 on BolaLab and 0.891 on Oqtane —
    ownership and identity recognition rely on names (`UserId`, `ICurrentUser.UserId`). Renaming identity members can even
    turn a protected bulk update into SYM-07 (owner predicate no longer recognised). Documented threat to validity.
26. **Membership-based authorization** is modelled heuristically (ADR-015): containers are found from navigations/join
    entities; tenant scoping by a `TenantId` value without a container entity (IoTSharp) still ends Unknown (SYM-09).
    Membership enforced inside a query predicate or filter the engine does not read can make SYM-11 over-report.
27. Projections are read sinks (ADR-015), but DTOs assembled outside EF (services that map entities by hand, lists of
    objects carrying ids in the request body) are not traced.
28. Custom access filters are trusted by name (ADR-014); role names in an owner-or-admin check are trusted as privileged.
29. `analysis.preferredImplementations` picks one implementation family per target (e.g. Bitwarden EF); the other family is not analysed.
30. Very large solutions (Jellyfin, Bitwarden, ErsatzTV) need 3–6 GB RAM and 5–6 min; Jellyfin hits the call-depth limit (518 Unknowns).
31. Access checks are trusted by name and binding (ADR-015, delegated SYM-05); a permissive branch anywhere in a helper
    (`authorized = true` for another entity type) rejects the whole helper — branch-insensitive (Oqtane A-004 fixed FP).
32. Creation-time BOLA (creating an object inside another user's container, Bitwarden CVE-2026-119414/43638) is not a sink.
