# ADR-015: Access-check calls are delegated guards; membership-scoped objects need one (SYM-11)
Date: 2026-11-23 · Status: Accepted (evaluate on labelled Dataset B before the freeze)

## Context
Round 2 of Dataset A showed that most missed advisories are not ownership checks (`x.UserId == caller`) but **membership**
checks: a user may act on an object because they belong to its library (Kavita), organization or collection (Bitwarden),
or tenant (IoTSharp). The code expresses that as calls — `HasAccessToSeries(userId, seriesId)`,
`_userPermissions.IsAuthorized(User, site, entity, entityId, perm)`, `_currentContext.OrganizationUser(orgId)` — and the
schema expresses it as relations (`Library.AppUsers`, `OrganizationUser{UserId, OrganizationId}`). Before this decision such
objects were Unknown (SYM-09: no owner candidate) and their fixes were invisible.

## Decision
1. **Access-check call** = bool/`Task<bool>` call whose name matches `analysis.accessCheckMethods` and which binds the caller
   (identity or principal argument, or a receiver type in `analysis.accessContextTypes`) to the tracked object (a loaded entity,
   a property of it, or the client id itself). Used as a guard it is **delegated authorization (SYM-05)**. A local helper that
   forwards a parameter and the caller to such a call is summarised the same way; if it can return `true` without the call
   (Oqtane CVE-2024-55470) it is rejected.
2. **Membership containers** are entities users belong to (collection navigation to/from the user type, or a join entity named
   after the container with a user owner). Entities that reach a container through reference navigations (≤ 4 hops) or a
   `{Container}Id` key are **membership-scoped** (`EntityModel.MembershipPath`).
3. A membership-scoped object without owner candidates, selected by a client id and changed (or read, in read mode) without
   a valid access check, is **Vulnerable — SYM-11** (previously Unknown).
4. Projections (`Select`, `SelectMany`, AutoMapper `ProjectTo`) keep the query: projected rows are read sinks, never EF writes.

## Consequences
+ Recall on membership-based advisories becomes measurable instead of abstaining; fixes that add an access check are seen.
− Access checks are trusted by name and binding, not by their body (delegated, listed separately, SLM candidates).
− SYM-11 can over-report where membership is enforced by a query filter or projection predicate the engine does not read
  (e.g. `Where(c => userLibraryIds.Contains(c.LibraryId))`); measured on Dataset B labels before any claim.
