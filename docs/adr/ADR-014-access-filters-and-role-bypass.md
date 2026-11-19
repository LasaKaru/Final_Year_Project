# ADR-014: Custom access filters are delegated authorization; owner-or-admin checks are owner checks
Date: 2026-11-19 · Status: Accepted (revisit with labelled Dataset B)

## Context
The second dataset round (Kavita, Jellyfin, Bitwarden advisories) showed three authorization shapes the engine rejected,
turning every *fixed* commit into a false positive:
1. **Custom filter attributes** — Kavita fixes CVE-2026-44776 and GHSA-6cpx with `[ChapterAccess]`, `[VolumeAccess]`,
   `[ReadingListAccess]` (classes deriving `AccessAttribute : IAsyncAuthorizationFilter` that check the route id).
2. **Owner or admin** — `if (tag.AppUserId != UserId && !User.IsInRole(Admin)) return Forbid();` (GHSA-8qw5 fix).
3. **Identity behind a base-controller property** — `protected int UserId => UserContext.GetUserIdOrThrow();`.

## Decision
1. An action/controller attribute declared in the analysed code that implements an MVC authorization or action filter
   interface and whose name matches `analysis.accessFilterPatterns` (default `Access`, `Authori[sz]`, `Permission`, `Owner`)
   makes the endpoint **Protected (SYM-05, delegated)** — like resource-based `AuthorizeAsync`, it stays an SLM candidate.
2. A combined condition whose operands are owner checks on the same object plus **role tests with the granting polarity**
   (`!IsInRole` in a deny-AND, `IsInRole` in an allow-OR) is an owner check: admins are vertical privilege, not peer users.
3. Getters of simple-typed properties in the analysed code are summarised once; a getter returning a recognised caller
   identity is that identity.

## Consequences
+ Kavita A-005: 9/9 units correct (5 vulnerable flagged, 3 fixed passed, 1 still-unfixed flagged at both commits).
− (1) trusts the filter without reading it: a filter named `*Access` that checks nothing hides a finding. Mitigation: the
  verdict is "delegated", listed separately, and the names are configurable. (2) trusts any role name as privileged.
