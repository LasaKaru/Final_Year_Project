# Error analysis — working log (symbolic arm)

Every false positive / false negative on a labelled unit gets a row; the category drives the next analyser change or a
documented limitation. Labels are still **drafts** (vendor-fix derived, `eval/labels/drafts`), so this is preliminary.
Categories: **G** guard pattern not recognised · **P** permissive/incorrect guard accepted · **S** scope/model limit
(field-level, vertical, tenant) · **D** DI/dispatch unresolved · **O** ownership schema heuristic · **L** loading/compile.

## Fixed during this iteration (2026-11-19) — found on Dataset A, then covered by unit tests

| Case | Symptom | Cause | Change (test) |
|---|---|---|---|
| A-002 fixed GET/DELETE | FP | **G**: `IsAuthorized(n.FromUserId) \|\| IsAuthorized(n.ToUserId)` rejected as "combined with other conditions" | OR of owner checks on the same object = one owner check (`Owner_checks_joined_by_or_…`, `Negated_owner_checks_joined_by_and_…`, `Owner_check_or_any_other_condition_stays_rejected`) |
| A-002 fixed GET | FP | **G**: repository returns `Find(id)` or `AsNoTracking().FirstOrDefault(…)`; only the first token was kept | alternative returned objects of one type are aliased (`Delete_by_id_after_an_or_check_…`) |
| A-002 fixed DELETE | FP | **G**: check on `Get(id)`, then `Delete(id)` → `Find(id)` + `Remove` (a second load of the same row) | two single loads by the same key value are the same row (key-tag aliasing) |
| A-002 vuln (would have become FN after the OR change) | — | **P**: `bool authorized = true; if (userid != null) …` — the CVE-2024-55186 flaw | helper that can return `true` without the comparison is rejected with its own reason (`Helper_that_returns_true_for_a_null_id_…`) |
| B-003 | 19 noisy Vulnerables | **S**: endpoints only `Host Users`/`Administrators` can call | ADR-013 / SYM-10, per-target `privilegedRoles` (`Endpoint_only_for_configured_privileged_roles_…`) |

## Open errors

| Case | Unit | Outcome | Category | Explanation | Plan |
|---|---|---|---|---|---|
| A-003 vuln | `GET api/user/{id}` | FN (predicted OutOfScope) | **S** | Private *settings* of another user are copied into the returned object; the object itself may be read. Field-level exposure is outside the model (ADR-011). | Keep as an expected miss; discuss in the dissertation (scope of BOLA vs. BOPLA / excessive data exposure, OWASP API3). |
| A-004 fixed | `GET api/setting`, `GET api/setting/{id}/{entityName}` | FP ×2 | **G** | Authorisation is a string-dispatched helper (`switch (entityName)`) calling a custom permission service `IUserPermissions.IsAuthorized(User, siteId, entityName, entityId, permission)`; the self-access case compares the caller with the *request* `entityId`, which is the owner key of `Setting` rows for entity `User`. | Candidate: `analysis.permissionServices` (calls treated like resource-based authorization, SYM-05, delegated → SLM). Until then the A-004 pair is **not discriminated** — the vulnerable variant's TP is not credited in the pair metric. |
| A-004 vuln | (same units) | TP | — | Flagged, but for the wrong reason (nothing recognised in either commit). | Report honestly: pair metric 0/2 for A-004. |

## Observations on Dataset B (unlabelled — no FP/FN claims yet)

* **B-001 Conduit**: 15 endpoints, 3 Protected (article PUT/DELETE, comment DELETE via author checks), 9 OutOfScope
  (MediatR handlers resolved; slugs are selectors). 0 Vulnerable. Labels needed to know whether any check is missed.
* **B-002 CleanArchitecture**: 5 Vulnerable SYM-08 on TodoList/TodoItem mutations. The template has no per-user ownership
  policy (CreatedBy is an audit field); whether these are true positives depends on the *intended* policy — the label
  protocol's q2 must be answered from the docs, and probably yields OutOfScope (single-tenant sample app).
* **B-003 Oqtane v10.3.2**: 33 Vulnerable after SYM-10; many use permission *policies* (`[Authorize(Policy = "EditModule")]`)
  whose handlers check the module id from the query string — the policy map does not interpret custom handlers yet, so
  these are likely FPs of category **G** (custom authorization handler). Needs labelling before any claim.
* 2 of 3 Oqtane projects report compile errors: all are Razor-generated component types (source generators are not run by
  MSBuildWorkspace 4.14) — API controllers are unaffected (known limitation).
