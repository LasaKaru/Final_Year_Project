# F — Critical evaluation of development progress (IPD section F)

## Significant achievements

1. **A working, EF-aware BOLA analyser for ASP.NET Core** — not a mock-up: 9 projects, ~7,800 lines of product code, 197 tests, CI.
2. **Real CVEs detected that a mainstream SAST tool misses:** precision 1.000 and recall 0.731 on 14 published advisories (draft
   labels); CodeQL `security-extended` found 0 of the 33 vulnerable units. 13 of 25 vulnerable → fixed pairs are told apart.
3. **Explainable output:** every finding carries the traced path, the guards found and *why* each was rejected, a rule ID, and a
   call graph in the local UI.
4. **Honest abstention:** missing evidence gives Unknown with a reason (5 abstentions on Dataset A), never "safe".
5. **Fully offline hybrid pipeline** with a local SLM, calibration gate and deterministic decoding — proven in a network-less run.
6. **Reproducible experiments:** 27 cases at pinned commits, every run recorded with commit, configuration hash and resources.

## Challenges and how they were addressed

| Challenge | How it was addressed | Evidence |
|---|---|---|
| Real code hides the owner check in helpers, OR-conditions, base-controller getters, permission services, filter attributes | each pattern found on a real case → reproduced as a unit test → recognised; permissive helpers explicitly rejected | ADR-014/015, `MembershipPatternsTests`, `RequestEntityPatternsTests` |
| The same database row is loaded twice (Get, then Find in Delete) | entity tokens with key-tag aliasing | ADR-008 |
| A check inside an unrelated `if` must not protect a later change | syntactic dominance (found one regression on Oqtane, fixed before recording) | ADR-016, `DominanceTests` |
| Organisation / library membership instead of per-user owners | membership paths in the schema mapper, rule SYM-11 | Kavita A-013: 7/7 pairs |
| .NET 10 / C# 14 targets fail to parse | `LanguageVersion.Preview` for net10.0 projects | `LanguageVersionTests` |
| Tiny model always answers "vulnerable"; Phi-3 escalation added FPs | calibration gate (ADR-009) and conservative fusion (ADR-012) | BolaLab runs |
| Running out of memory with scan + model in one 15 GB container | `--semantic-units` limits the model to labelled units; heavy cases moved to the laptop | `summary-hybrid.md` |
| Stale build used by parallel recorded runs | rule: rebuild before `--no-build`; development in separate worktrees | `CLAUDE.md` |

## Changes in methodology / technology and why

* From "SLM decides" to "**SLM may only suppress**" — measured, not assumed (ADR-012).
* From guard windows to **dominance** — soundness for exit-style checks (ADR-016).
* From a React dashboard to a **local, framework-free UI** — offline rule (ADR-010).
* Baseline chosen: **CodeQL C#** run in GitHub Actions (the container could not download the CLI).

## Limitations and outstanding issues

| Limitation | Impact | Plan |
|---|---|---|
| Labels are drafts derived from vendor fixes | all A/C numbers are preliminary | owner verification in December; freeze 21 Mar |
| Dataset B unlabelled (1,234 Unknown verdicts in wild code) | precision on wild code unknown | blind labelling, second rater, Cohen's κ |
| Owner inference partly name-based (P4 stability 0.313) | renamed owner properties are missed | FK evidence + configuration; report as threat to validity |
| Creation-time BOLA, tenant scoping by value, custom policy handlers | misses on Bitwarden, IoTSharp; noise on Oqtane B-003 | engineering items before the freeze |
| Branch-specific permissive values (Oqtane A-004 fixed) | 2 FPs on Dataset C | path-sensitive helper summaries (S6) |
| Field-level exposure (Oqtane A-003) | out of the BOLA model | discuss as BOPLA (API3) in the dissertation |
| Syntactic, not CFG, dominance; `goto`/exceptions not modelled | rare in controllers | Roslyn `ControlFlowGraph` only if data shows the need |
| SLM adds no precision so far (RQ2) | the hybrid stage's value is the rationale, not FP reduction | Qwen2.5-Coder comparison, τ calibration; report honestly |
| .NET 8 end of support (10 Nov 2026) | security patches for the *tool's* runtime | offline research prototype; one-line switch to .NET 10 (ADR-001) |
| Windows not yet tested (NFR09) | portability claim partial | January |

## Is the project on track?

**Yes for the software, conditionally for the research.** Development is ~8 weeks ahead and the remaining engineering items are
improvements, not missing features. The critical path now runs through owner actions — ethics, label verification, blind labelling
with a second rater, the RQ4 rating study and the dissertation. These are scheduled (Dec – Jun) with the label freeze on 21 Mar as
the main checkpoint. The biggest academic risk is **R6 (owner understanding of AI-assisted code)**; mitigation: a class-by-class
study guide ([`../09-qa/class-guide.md`](../09-qa/class-guide.md)), every use logged in `docs/ai-log.md`, and rehearsed code walkthroughs.
