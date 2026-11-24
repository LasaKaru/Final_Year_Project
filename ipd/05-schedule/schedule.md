# E — Updated project schedule (IPD section E)

* Current Gantt chart: [`../diagrams/gantt.png`](../diagrams/gantt.png) (red line = 24 Nov 2026; red bars = critical path).
* Baseline (plan of 18 Nov 2026, after the PP): [`../diagrams/gantt-baseline-2026-11-18.png`](../diagrams/gantt-baseline-2026-11-18.png).
* Detailed deviation log: [`schedule-deviations.md`](schedule-deviations.md). Update both before the IPD (status line in `CLAUDE.md`).

## Completed since the PP

| Activity | Planned (baseline) | Actual |
|---|---|---|
| Repository, CI, BolaLab fixture TC01–TC16 | S0, by 29 Nov | done 19 Nov |
| Symbolic engine: endpoints, policy, schema, DI, taint walker, sinks, guards, rules | S1–S3, 30 Nov – 10 Jan | done 19 Nov; hardened on real repositories to 23 Nov |
| IR slices, ONNX validator, hybrid mode, evaluation harness | S3b–S4, 1 – 20 Jan | done 19 Nov |
| SARIF, GitHub Action, robustness runner, web UI | M4, April | done 19 Nov |
| Datasets A/B/C: framework + 27 recorded cases | Dec – 21 Mar | 14 A-cases (25 vulnerable → fixed pairs), 13 B-repositories recorded by 23 Nov |
| CodeQL baseline arm | "to choose" | run in GitHub Actions, imported (0/33) |
| Phi-3-mini hybrid experiment (RQ2) | S8, March | 15 reports recorded 23 Nov (negative result) |
| Syntactic dominance (S6 step 1) | Feb – Mar | done 23 Nov (ADR-016) |

## Ongoing and pending

| Activity | Owner | Target |
|---|---|---|
| Ethics Part A | student | **overdue (planned 27 Nov)** — critical |
| Verify draft labels of Datasets A/C | student | Dec 2026 |
| Blind-label Dataset B + second-rater sample | student + second rater | Dec – Mar (frozen 21 Mar 2027) |
| Read and re-explain every class (risk R6) | student | continuous, by 15 Jan |
| Supervisor review of ADR-011 … ADR-016 (scope changes) | supervisor | early Dec |
| Tenant scoping by value, creation-time BOLA, custom policy handlers | engineering | by 10 Jan |
| Qwen2.5-Coder + Phi-3 on the 16 GB laptop (Bitwarden, Jellyfin) | engineering | by 10 Jan |
| Windows run (NFR09) | engineering | Jan |
| Code freeze `v0.1-ipd` | — | 20 Jan 2027 |
| Slides, three rehearsals, backup video | student | 5 – 27 Jan |
| IPD submission | — | **28 Jan 2027** |

## Milestones after the IPD

| Milestone | Date |
|---|---|
| Live IPD + Q&A | 29 Jan – 19 Feb 2027 |
| Labels frozen; calibration split defined | 21 Mar 2027 |
| τ calibration + model comparison (S8) | 10 Apr 2027 |
| Final experiments (3 configurations × datasets × 3 repetitions) + RQ4 rubric study | 10 May 2027 |
| Dissertation chapters | mid-Mar – 5 Jun 2027 |
| Final submission + viva | 15 Jun 2027 |

## Deviations and their causes

| Deviation | Direction | Cause | Consequence |
|---|---|---|---|
| Engine, SLM stage, reporting finished ~8 weeks early | earlier | AI-assisted implementation (every use logged in `docs/ai-log.md`) | time re-planned into datasets, real-repo hardening and the owner's code study (R6) |
| Datasets advanced from "framework" to 27 recorded cases | earlier | tooling (`run-case.sh`, `dataset-report.py`) made each case cheap | labels are still **drafts** — verification is now the bottleneck |
| Scope widened: read BOLA, privileged roles, membership | changed | real advisories (ADR-011/013/015) | more rules to explain; documented per ADR |
| Ethics Part A not signed | **late** | owner action outstanding | blocks any user feedback / second-rater study; first action this week |
| Hybrid runs on Bitwarden/Jellyfin not done | late | memory: 5–7.5 GB scan + 6–9 GB model > 15 GB container | moved to the 16 GB laptop in December |
| Model comparison (Qwen2.5-Coder) | unchanged | planned after the label freeze anyway | — |

## Critical judgement: are we on schedule?

**Engineering is ahead of plan** (S0–S4 and most of M4 done ~8 weeks early). **The evaluation and documentation tracks are on or
slightly behind plan** because they depend on owner actions that cannot be delegated: ethics, label verification, blind labelling with
a second rater, and the dissertation itself. The realistic risk to the final deadline is not code but *labels and writing*; the
schedule therefore moves engineering effort to a supporting role after the code freeze and protects March for labelling.
