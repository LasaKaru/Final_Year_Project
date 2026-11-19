# 10 — Daily Workflow, Git Rules, Logs & Templates

Your GitHub history, logs and decision records are your **evidence of independent ownership** — the IPD explicitly assesses it, and the Q&A can be extended to check authenticity. Treat these habits as part of the project, not overhead.

---

## 1. Git workflow

- `main` is always green (protected; merge via PR after CI passes — self-review is fine, but write a PR description).
- One branch per backlog task: `feature/S2-03-taint-walker`, `fix/S2-05-ef-chain-receiver`, `docs/S1-10-stakeholders`.
- **Commit small and often** (at least once per working session). Never one giant commit per sprint.
- Tags: `v0.1-ipd` (20 Jan), `labels-frozen-YYYYMMDD`, `v0.2-m3` (31 Mar), `v1.0-final`.

### Commit message style (Conventional Commits)
```
feat(taint): propagate tainted ids into callee parameters (#21)
fix(schema): read HasQueryFilter lambdas from IEntityTypeConfiguration (#18)
test(bolalab): golden test for TC13 cross-layer void (#25)
docs(adr): ADR-004 SLM validates only symbolic candidates
chore(ci): build fixture before tests
```
Types: `feat`, `fix`, `test`, `refactor`, `docs`, `chore`, `perf`, `data`.

### Pull request template (`.github/pull_request_template.md`)
```markdown
## What
## Why (issue #)
## How I tested
- [ ] unit tests  - [ ] golden tests  - [ ] manual run on BolaLab
## Notes / limitations
```

---

## 2. Daily routine (per session)

1. Pull, open the board, pick **one** task.
2. Write/adjust a failing test first when possible (TDD fits analysers very well: snippet in → expected record out).
3. Implement until green; run `dotnet test`.
4. Commit + push; move card.
5. 2-minute note in `docs/weekly-log.md` (what/why/problem).
6. If AI helped: one line in `docs/ai-log.md`.

---

## 3. Weekly log template (`docs/weekly-log.md`)

```markdown
## Week of 2026-11-30 (Sprint 1, week 1)
**Hours:** 19
**Done:** S1-01 core models; S1-02 endpoint enumerator (routes + params)
**Problems:** `[controller]` token not replaced for nested route attribute → fixed by combining class+method templates
**Decisions:** ADR-008 normalise routes to lower case without constraints
**Next week top 3:** S1-03 policy mapper; S1-04 schema mapper; S1-06 TC05–TC08
**Risks:** none new / R3 dataset: 0 admitted so far
```

---

## 4. ADR template (`docs/adr/ADR-00N-title.md`)

```markdown
# ADR-004: The SLM validates only symbolic candidates
Date: 2026-12-28 · Status: Accepted
## Context
RQ2 compares hybrid vs symbolic-only. If the SLM saw different units, differences could come from coverage, not reasoning.
## Decision
Invoke the SLM only on paths the symbolic stage marks Vulnerable, Unknown, or Protected-by-delegation. Same candidates, same labels in both arms.
## Consequences
+ Clean ablation; lower inference cost. − SLM cannot discover paths the symbolic stage missed (recall bounded by Stage 1) — reported as a limitation.
## Alternatives considered
SLM on every endpoint (costly, confounded); SLM-only baseline (kept as optional exploratory arm).
```
Initial ADRs to write in Sprint 0: ADR-001 .NET 10, ADR-002 CLI-first, ADR-003 three-state verdict, ADR-004 candidates-only SLM, ADR-005 BolaLab fixtures, ADR-006 labels outside code, ADR-007 greedy decoding + grounding + retry.

---

## 5. Sprint report to supervisor (one page, every 2 weeks)

```markdown
# Sprint S1 report (30 Nov – 13 Dec 2026) — Lasantha Karunarathne
**Sprint goal:** Context extraction (endpoints, policy, schema) — ✅ met / 🔶 partly / ❌ missed
**Completed:** …  (links to PRs / commits)
**Not completed & why:** …
**Demo:** `bolalens schema fixtures/BolaLab/BolaLab.sln` → screenshot/link
**Metrics:** tests: 47 passing · BolaLab TCs implemented: 16/16 · dataset screened: 34, admitted: 0
**Risks / blockers:** …
**Decisions needing your input:** …
**Next sprint goal:** Path extraction (DI + taint + sinks + guards)
```

### Supervisor meeting minutes (`docs/supervisor-meetings.md`)
```markdown
## 2026-11-26 — Meeting 1
Attendees: Ms. Pudara Dewdini, Lasantha
Discussed: plan, .NET 10, IPD submission format, ethics form
Feedback: …
Actions: [ ] Lasantha — … by …   [ ] Supervisor — sign ethics form
```

---

## 6. First email to your supervisor (send today)

> **Subject:** FYP (BOLA detection) — implementation plan to IPD and four quick questions
>
> Dear Ms. Dewdini,
>
> I have prepared a sprint plan from now until the IPD submission on 28 January 2027. Implementation starts this week: a .NET solution with CI, a small purpose-built vulnerable API (16 labelled test cases) as the regression suite, then the Roslyn symbolic engine in December and a first local-SLM integration in early January, with a code freeze on 20 January.
>
> Could you please advise on:
> 1. A suitable time this week or next for a short meeting?
> 2. What exactly should be submitted on 28 January (slides only, or also a document, repository link or video)?
> 3. Since .NET 8 and .NET 9 reached end of support on 10 November 2026, I plan to build the tool on .NET 10 (LTS) while still analysing projects that target .NET 8/9. Is that acceptable?
> 4. Signing the ethics Part A form (no human participants; public code only) — I will send the completed form this week.
>
> I will send a one-page progress report at the end of each two-week sprint. I would also like to give you read access to my private GitHub repository — could you share your GitHub username?
>
> Kind regards,
> Lasantha Karunarathne (IIT 20223087 | UoW W19598693)

---

## 7. AI-use log (`docs/ai-log.md`) — keep it accurate

Your own October methodology note says the PP's "AI only for grammar" statement must not be repeated. The safest position: **use AI openly, log it precisely, verify everything, and be able to explain every line.**

```markdown
| Date | Tool | Purpose | What I used | How I verified / changed it |
|---|---|---|---|---|
| 2026-11-18 | Claude | Planning | Sprint plan + guide pack (structure, task lists, code skeletons) | Reviewed with supervisor; adapted dates; skeletons retyped and tested |
| 2026-12-05 | Claude | Debugging | Explanation of extension-method receiver in IOperation | Confirmed in Roslyn docs; wrote my own test |
```
Rules you set for yourself:
- No AI-generated **labels** (ground truth is human).
- No AI-generated **results** or numbers.
- Any AI-suggested code is rewritten/understood, tested, and logged.
- Never paste non-public third-party code or personal data into external AI tools (your analysed repos are public, but follow the habit).

---

## 8. Definition of Done (applies to every coding task)

- [ ] Code compiles without new warnings
- [ ] Unit tests added/updated and passing; golden tests still green
- [ ] Public classes have a 3-line doc comment (purpose · input · known limitation)
- [ ] CLI help text updated if a command changed
- [ ] Known limitation recorded in `docs/known-limitations.md` if relevant
- [ ] Committed, pushed, PR merged, card moved, weekly log updated

---

## 9. Reproducibility file (`docs/reproducibility.md`)

```markdown
## Environment
OS / CPU / RAM / GPU:
.NET SDK: 10.0.xxx (dotnet --info)
Packages: (copy from Directory.Packages.props)
Model: phi3-mini-4k-instruct cpu-int4-rtn-block-32-acc-level-4 · source URL · licence · SHA-256 of each file
Prompt: v1 (sha-256 of prompt-v1.txt)
Config: bolalens.json sha-256
## How to reproduce a run
git checkout <tag>
dotnet build -c Release
dotnet run --project src/BolaLens.Cli -c Release -- scan <target> --mode hybrid --out eval/runs/<id>/report.json
dotnet run --project src/BolaLens.Cli -c Release -- eval --report … --labels … --out eval/runs/<id>/metrics.md
```
