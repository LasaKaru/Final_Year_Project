# 07 — Dataset Acquisition, Admission & Labelling Guide (parallel track P1)

This is the highest *research* risk (R3). Start in Sprint 1 and spend 2–3 h every week. Everything here follows the rules you already set in your October gap/methodology documents — this file turns them into a procedure.

---

## 1. Principles (repeat these at the IPD if asked about validity)

1. **Three separate groups, never pooled:** A = historical vulnerable/fixed pairs, B = real open-source APIs, C = verified secure controls. BolaLab and reconstructed cases are a **fourth, separately reported** "controlled fixtures" group.
2. **Admission before analysis:** a case enters only with a pinned commit, licence, reproducible build, and an EF Core path.
3. **Labels before predictions:** labels are written and frozen *before* you look at what the tool says about that repo (avoid tool-defined ground truth).
4. **Split by repository family:** forks, vulnerable/fixed pairs and derived variants stay together; calibration and held-out partitions are separated before prompt/threshold tuning.
5. **Honest denominators:** every eligible unit is counted, including the ones that failed to build or came out Unknown.

---

## 2. Search log (start S1-09)

`eval/datasets/search-log.md` — one row per search:

| Date | Source | Query / filter | Results seen | Candidates kept | Notes |
|---|---|---|---|---|---|
| 2026-12-01 | GitHub Advisory DB | ecosystem NuGet + CWE-639 | 23 | 2 | most are libraries, not apps |

This log is evidence of a systematic, reproducible search — exactly what an examiner asks for when Dataset A turns out small.

---

## 3. Dataset A — historical vulnerable/fixed pairs

### Where to search
| Source | How |
|---|---|
| **GitHub Advisory Database** (github.com/advisories) | Filter ecosystem **NuGet**; search CWEs **639** (Authorization Bypass Through User-Controlled Key), **566** (…User-Controlled SQL Primary Key), **862** (Missing Authorization), **863** (Incorrect Authorization), **285** (Improper Authorization), **284** (Improper Access Control); keywords "IDOR", "insecure direct object", "access other users". |
| **OSV.dev** | Ecosystem NuGet, same keywords. |
| **NVD** (nvd.nist.gov) | Keyword search "IDOR" + "ASP.NET" / ".NET"; CWE-639 filter. |
| **Open-source app repos' Security tabs** | Many C# apps publish advisories in their own repo (Security → Advisories). |
| **huntr.com / bug-bounty write-ups** | Search IDOR reports against C# repositories. |
| **GitHub commit search** | `IDOR language:C#`, `"authorization bypass" language:C#`, `"check ownership" language:C#`, `"belongs to" fix language:C#` — then inspect the diff. |

### Screening questions (reject fast)
1. Is it an **application** (not a library)?
2. ASP.NET Core controllers (API or MVC) + **EF Core** on the vulnerable path?
3. Is it **horizontal, state-changing** (update/delete/state change on another user's/tenant's object)? Read-only disclosure → record as "out of primary scope".
4. Can you identify the **fix commit** and the **vulnerable parent commit**?
5. Does the vulnerable commit **build** with an available SDK (8/9/10)?

> Scope note to raise with your supervisor: many real C# apps use MVC controllers (`Controller : ControllerBase`) rather than pure Web API. The analyser handles both. Agreeing to "ASP.NET Core controller endpoints (API and MVC)" may double your candidate pool without changing the method.

### Expectation & fallback
Expect **few** admissible .NET cases (single digits). That is a finding, not a failure. If fewer than ~5 by 21 March:
- keep the ones you have, report them case by case (no aggregate claim);
- add **reconstructed cases**: port a documented advisory's vulnerable pattern into a small buildable project → label as `reconstructed`, report separately, never count as "historical CVE".

---

## 4. Dataset B — real open-source ASP.NET Core + EF Core APIs

### Selection criteria
- Public, OSI licence, active within ~2 years, ≥ 3 controllers, ≥ 1 `DbContext`, user/tenant-owned resources with route IDs.
- Builds on .NET 8/9/10 with `dotnet restore && dotnet build` (allow small, documented tweaks such as removing a missing private feed).
- Not a tutorial with 2 endpoints.

### How to find candidates
- GitHub code search: `"HasQueryFilter" language:C#`, `"[HttpPut(\"{id}\")]" "DbContext" language:C#`, `"FindFirstValue(ClaimTypes.NameIdentifier)" language:C#`.
- GitHub topics: `aspnetcore-webapi`, `aspnet-core`, `clean-architecture`, `multi-tenant`, `efcore`.
- Leads mentioned in your literature (e.g. C# applications listed in the Neo paper's corpus) and well-known .NET reference/community apps (eShop variants, RealWorld "Conduit" ASP.NET Core implementation, Clean Architecture templates, self-hosted apps like media/reading servers). **These are leads to screen, not admitted cases** — check EF Core usage, ownership model and build first.

Target: **10–15 shortlisted**, **4–8 admitted** by 21 March. For the IPD you need **one** that builds and scans.

---

## 5. Dataset C — verified secure controls

- Candidates: well-maintained apps/reference projects where ownership/tenant checks are systematic (filters + owner checks).
- Your October review is right: a famous reference app (e.g. eShopOnWeb) is **only a candidate** — the original repo now points to a community successor, and every relevant path must be audited before it counts as "secure".
- A protected path in Dataset B can also serve as a negative unit; Dataset C exists to measure false alarms on code that is *systematically* protected.

---

## 6. Admission checklist (`eval/datasets/admission-checklist.md`)

| # | Evidence | A | B | C |
|---|---|---|---|---|
| 1 | Repository URL + immutable commit SHA | ✔ | ✔ | ✔ |
| 2 | Licence recorded and compatible with analysis/reporting | ✔ | ✔ | ✔ |
| 3 | Target framework, EF Core version, build command, build log | ✔ | ✔ | ✔ |
| 4 | Advisory ID + fix commit + vulnerable parent commit | ✔ | — | — |
| 5 | Intended policy documented (who may change what) | ✔ | ✔ | ✔ |
| 6 | Endpoint inventory created independently of the detector | ✔ | ✔ | ✔ |
| 7 | Secrets/personal-data check before storing any excerpt | ✔ | ✔ | ✔ |
| 8 | Repository family ID (for splitting) | ✔ | ✔ | ✔ |
| 9 | Decision: admitted / rejected / quarantined + reason | ✔ | ✔ | ✔ |

Do **not** copy whole third-party repos into your repository — store URL + SHA and clone on demand with a script (`eval/scripts/fetch.ps1|sh`).

---

## 7. Registry (`eval/datasets/registry.csv`)

```
case_id,group,family_id,repo_url,commit_sha,parent_sha,advisory,licence,tfm,ef_version,build_ok,status,reason,added_on
A-001,A,fam-xyz,https://github.com/org/app,abc123,def456,GHSA-xxxx,MIT,net8.0,8.0.x,yes,admitted,,2026-12-10
B-001,B,fam-conduit,https://github.com/org/conduit,9f8e7d,,,MIT,net8.0,8.0.x,yes,admitted,,2026-12-20
```

---

## 8. Labelling protocol

### 8.1 Unit
An **endpoint operation + its object-access path** at a pinned commit (same unit the tool reports). Multiple alerts for one unit are de-duplicated.

### 8.2 Labels (from your methodology document)
| Label | When |
|---|---|
| **Vulnerable** | A client-supplied identifier selects another principal's protected object, a state change is reachable, and no effective, operation-specific guard exists (considering filters and policies). |
| **Protected** | The intended policy is enforced on every modelled route to that operation. |
| **Unknown** | Intent or execution evidence is insufficient. |
| **OutOfScope** | Read-only, create-only, admin-only by design, or excluded framework. |

### 8.3 Procedure
1. **Inventory first:** list every in-scope endpoint by reading controllers (cross-check with `grep -rn "\[Http" --include=*.cs`). You may *also* run `bolalens endpoints`, but reconcile differences manually — the inventory must not depend on the detector.
2. **Policy first:** write 2–3 lines on intended ownership for each entity (README, docs, tests, UI behaviour, fixes).
3. Label each unit with a rationale + source lines, reviewer, date.
4. **Freeze** the label file (commit + tag `labels-frozen-YYYYMMDD`) **before** running the tool on that repo for evaluation (exploratory runs for debugging the tool are allowed only on calibration repos).
5. Disputed cases → ask a second technically qualified reviewer (supervisor / colleague); until then say "single-reviewer labels" honestly.
6. Optional dynamic confirmation for runnable cases (two identities, reset DB, before/after state) — label validation only.

### 8.4 Label file (`eval/labels/B-001.labels.json`)
```json
{
  "caseId": "B-001",
  "commit": "9f8e7d…",
  "reviewer": "LK",
  "frozenOn": "2027-03-21",
  "units": [
    { "verb": "PUT", "route": "api/articles/{slug}", "entity": "Article", "label": "Protected",
      "rationale": "Handler compares article.Author.Username with current user before update.",
      "evidence": ["src/Features/Articles/Edit.cs:45-52"] }
  ]
}
```

### 8.5 Splits
`eval/splits/calibration.txt` and `heldout.txt` list **family IDs**. Rule of thumb: ~20–30 % of families for calibration (prompt/threshold choice), the rest held out. BolaLab is development data, never held-out evaluation.

---

## 9. Milestones for this track

| By | Target |
|---|---|
| 13 Dec | Search log ≥ 30 screened items; registry created |
| 27 Dec | Dataset B shortlist (10–15); ≥ 2 build checks done |
| 10 Jan | ≥ 1 B repo admitted and scanned (symbolic) |
| 20 Jan | Exploratory labels on ~10 endpoints of that repo (marked *exploratory*, not frozen) |
| IPD | Show: search log, registry, admission checklist, one real-repo run with coverage + Unknowns |
| 21 Mar | **All labels frozen**, splits fixed |

---

## 10. Responsible disclosure (if you find a real, new vulnerability)

1. Stop; do not exploit beyond confirming on a **local** copy.
2. Check the project's `SECURITY.md`; use GitHub private vulnerability reporting if enabled; otherwise email maintainers.
3. Share minimal reproduction details; agree a disclosure timeline (commonly 90 days).
4. In the dissertation, describe it at pattern level until fixed/disclosed; inform your supervisor.
5. Log it in `docs/disclosures.md` (private notes stay out of the public repo).
