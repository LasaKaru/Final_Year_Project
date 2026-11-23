# RQ4 — explanation quality: rubric and protocol

**Question.** Do BolaLens explanations (verdict rationale, rule ID, traced path / call graph, checks with rejection reasons, IR slice
and — in hybrid mode — the model's grounded reason) let a developer understand and act on a finding?

**Sample.** `python3 scripts/rq4-sample.py --n 40 --seed 2027` draws a stratified sample from the recorded runs
(Vulnerable / Protected-delegated / Unknown, across Datasets A, B, C; BolaLab excluded) and writes
`eval/rq4/rating-sheet.csv` (+ a Markdown copy). The seed is recorded; never resample after seeing scores.

**Raters.** The project owner and one independent rater (supervisor or a peer with ASP.NET Core experience). Each rates
alone, with the source code open at the pinned commit (`../datasets/<case>-<variant>`) and the HTML report of that run.

| Criterion | 0 | 1 | 2 |
|---|---|---|---|
| **R1 Root cause** | wrong or missing | partly right (object or check, not both) | names the object selected by the client id and the missing/insufficient check |
| **R2 Location** | lines wrong | sink or guard line right | sink and guard/entry lines right and reachable from the evidence |
| **R3 Actionability** | no fix implied | generic fix | a concrete fix follows (which owner property to compare with which identity, where) |
| **R4 Honesty of Unknown** (Unknown only) | reason unhelpful | names the unresolved call | names it and says what would decide the case |
| **R5 Time to verdict** | minutes until the rater agrees/disagrees with the verdict (record the number) | | |

Also record **agree** (yes/no: does the rater agree with the verdict after reading the code?) — this doubles as a label
sanity check on Dataset B units.

**Analysis.** Mean and median per criterion, per verdict type and per dataset; Cohen's weighted κ between raters for
R1–R3; proportion of "agree". Report explanation failures as an error category in `eval/results/error-analysis.md`.

**Bias control.** Raters do not see dataset labels. The owner rates first, then the second rater; no discussion until both
sheets are saved. The script output (sample + seed) is committed before rating starts.
