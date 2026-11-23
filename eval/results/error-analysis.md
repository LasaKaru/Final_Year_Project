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

## Second dataset round (2026-11-19, commit 88ada44): Kavita, Jellyfin, Bitwarden, IoTSharp

### Fixed during this round — each found on a real case, then covered by a unit test
| Case | Symptom | Cause | Change (test) |
|---|---|---|---|
| A-005 (Kavita, .NET 10) | ~1,300 compile errors, unresolved calls | **L**: C# 14 extension blocks / `field` parsed as errors by Roslyn 4.14 | .NET 10 projects parsed with `LanguageVersion.Preview` (`LanguageVersionTests`) |
| A-005 | routes `update-dashboard-stream` without `api/stream` | **L**: `[Route]` on `BaseApiController` ignored | inherited `[Route]`/`[Authorize]` (`Route_and_authorize_inherited_from_a_base_controller_apply`) |
| A-005 fixed | owner check rejected ("comparand is not the caller") | **G**: `UserId => UserContext.GetUserIdOrThrow()` getter not evaluated | getter identity summary (`Base_controller_property_returning_the_caller_identity_is_trusted`) |
| A-005 | update-series / bulk endpoints without a path | **G**: `dto.Tag.Id`, `dto.Ids` + `ids.Contains(s.Id)` | nested/list ids (`Ids_nested_…`, `Bulk_update_…`) |
| A-005 fixed | `tag.AppUserId != UserId && !User.IsInRole(Admin)` rejected | **G**: owner-or-admin | role bypass with polarity check (`Owner_check_with_an_admin_role_bypass` ×4) |
| A-013 fixed | `[ChapterAccess]` endpoints | **G**: custom authorization filter attributes | delegated SYM-05 (ADR-014, `Custom_access_filter_attribute_…`) |
| A-008…A-012 | everything Unknown | **D**: Dapper + EF implementation of every repository | `analysis.preferredImplementations` per target (`Preferred_implementation_…`) |

### Open (expected) misses — the scope boundary
| Case | Outcome | Category | Explanation |
|---|---|---|---|
| A-006 Kavita reads | FN ×3 | **S** | reading-list items / bookmarks / annotations are returned as DTO projections (`ProjectTo`, `Select`) — read mode only models entity loads; bookmark ids are inside a list of objects |
| A-007 Jellyfin playlist | FN | **S/D** | items come from Jellyfin's library manager (not a direct EF query); the flaw is "userId query parameter used as the caller" |
| A-008…A-012 Bitwarden | Unknown / OutOfScope | **S** | authorization is organization/collection **membership** through `ICurrentContext.OrganizationUser(orgId)` (tenant-level, custom permission service), sinks behind command handlers and Dapper/EF stored procedures |
| A-013 Kavita downloads (vuln) | Unknown SYM-09 ×4, OutOfScope ×3 | **S** | library-membership model: `Chapter` has no owner property, so the tool abstains (honest Unknown); the fixed commit uses `[ChapterAccess]` (now Protected, SYM-05) |
| A-014 IoTSharp (vuln only) | FN ×3 | **S** | cross-**tenant** reads by GUID; restore failed (missing private feed packages) and reads are projections |

**Reading for the dissertation:** precision on Dataset A is 1.000 (every endpoint flagged in a vulnerable commit is a true
positive); recall is bounded by the ownership model (horizontal, user-owned objects). Membership-, tenant- and
library-scoped authorization need a different "owner" relation (user ∈ library/organization) — candidate future work (S6+).

## Round 3 (2026-11-23, commit b6db8a1): access checks, membership, projections — and the CodeQL baseline

| Change | Effect on Dataset A/C (draft labels) |
|---|---|
| access-check calls as delegated guards (ADR-015); permission-service helpers (permissive ones rejected) | fixed commits that add `HasAccessTo…`/`IsAuthorized(User, …)` are seen |
| membership-scoped objects (library / organization containers) → SYM-11 instead of SYM-09 | Kavita A-013: 7/7 vulnerable units flagged (was Unknown), fixed commit not flagged → 7 pairs discriminated |
| projections keep the query; condition-only loads are not read sinks; self-access checks; object-check helpers | no regressions; Oqtane A-004 fixed still FP (permissive branch in another `switch` case) |
| run-case.sh initialises git submodules | IoTSharp A-014 now restores: 1 TP + 2 honest Unknowns (was 3 FN on a broken build) |

**Totals:** Dataset A TP 19 / FN 7 / abstained 5 → precision 1.000, recall 0.731 (was 0.458), operational recall 0.576;
Dataset C TN 20 / FP 2 (FPR 0.091); 13 of 25 vulnerable→fixed pairs discriminated (was 6).

**Baseline (CodeQL 2.27.5 security-extended, run 45130132411):** 0 of 33 labelled vulnerable units flagged, 0 false
positives on the fixed commits. Its authorization queries (`cs/web/missing-function-level-access-control`,
`cs/web/insecure-direct-object-reference`) fire on other actions (login pages, unauthenticated endpoints) — they look for a
missing `[Authorize]` or a missing check on a whole action, not for an owner/membership binding of a client-selected object.

**Still open:** Kavita A-006 reads (reading list items via a service that returns DTOs built outside EF; bookmark ids
inside a list of objects); Jellyfin A-007 (library manager, not EF); Bitwarden A-008…A-012 (commands, creates and
organization-level stored procedures — creation-time BOLA is outside the model); Oqtane A-003 (field-level) and A-004 fixed.
