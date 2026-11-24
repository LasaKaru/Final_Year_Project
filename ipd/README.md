# IPD submission pack — BolaLens (6COSC023C, 2026/27)

Everything for the **Interim Progression Demonstration** is in this folder: submit by **Thu 28 Jan 2027**; live presentation, demo
and Q&A between 29 Jan and 19 Feb 2027 (20 min presentation + demo, ≈ 20 min Q&A, may be extended).
Status of all content: **24 Nov 2026** — refresh the numbers after the code freeze (20 Jan, tag `v0.1-ipd`).

> **Draft prepared with AI assistance (logged in `docs/ai-log.md`).** Before submitting, read every page, rewrite the slide text
> and speaker notes in your own words, and check every number against the recorded runs. You must be able to explain all of it.

## Start here

| # | Folder / file | Brief section | What it contains |
|---|---|---|---|
| — | [`00-brief/`](00-brief) | — | the module brief, submission requirements, [**submission checklist**](00-brief/submission-checklist.md) |
| 1 | [`01-slides/BolaLens-IPD.pptx`](01-slides/BolaLens-IPD.pptx) (+ [PDF](01-slides/BolaLens-IPD.pdf)) | all | 19 slides (≈ 12 min) + demo slide + 5 appendix slides; [speaker notes](01-slides/speaker-notes.md); rebuild: [`01-slides/src`](01-slides/src) |
| 2 | [`02-stakeholders/stakeholders.md`](02-stakeholders/stakeholders.md) | B | onion model, roles/interests/expectations, how stakeholders changed the project |
| 3 | [`03-requirements/requirements.md`](03-requirements/requirements.md) | C | 21 FR + 12 NFR with ✅/🔶/⬜ status and evidence, use cases, changes since the PP, traceability |
| 4 | [`04-architecture/architecture-and-design.md`](04-architecture/architecture-and-design.md) | D | architecture, components, technology justification, core algorithm, rules, AI pipeline, metric formulas, data organisation, design changes |
| 5 | [`05-schedule/schedule.md`](05-schedule/schedule.md) | E | updated Gantt, completed / pending work, milestones, deviations, on-track judgement |
| 6 | [`06-results/preliminary-results.md`](06-results/preliminary-results.md) | demo: testing | tests, BolaLab, Dataset A/C vs CodeQL, RQ2 (SLM), Dataset B, robustness, performance |
| 7 | [`07-critical-evaluation/critical-evaluation.md`](07-critical-evaluation/critical-evaluation.md) | F | achievements, challenges → solutions, limitations, is the project on track |
| 8 | [`08-demo/`](08-demo) | Part B | [demo script](08-demo/demo-script.md) (8 min), [`demo-prep.sh`](08-demo/demo-prep.sh) (build + tests + scan check), [backup video plan](08-demo/backup-video-plan.md) |
| 9 | [`09-qa/`](09-qa) | Stage 2 | [Q&A preparation](09-qa/qa-preparation.md), [class guide](09-qa/class-guide.md) (all 153 types — study list for risk R6), [evidence map](09-qa/qa-evidence-map.md) |
| — | [`diagrams/`](diagrams) | C, D, E | rendered PNG + SVG; sources in [`diagrams/src`](diagrams/src) (`render.sh`) |
| — | [`screenshots/`](screenshots) | D (UI) | the real web UI: dashboard, findings, call graph, phone, dark mode |

## Diagrams

| Diagram | Type | File |
|---|---|---|
| Stakeholder onion | stakeholder model | [`stakeholder-onion.png`](diagrams/stakeholder-onion.png) |
| Use cases (status-coloured) | UML use case | [`use-cases.png`](diagrams/use-cases.png) |
| Components | component / architecture | [`components.png`](diagrams/components.png) |
| Scan pipeline | activity / data flow | [`pipeline.png`](diagrams/pipeline.png) |
| Core algorithm | activity (decision flow) | [`algorithm.png`](diagrams/algorithm.png), landscape [`algorithm-lr.png`](diagrams/algorithm-lr.png) |
| Hybrid scan | UML sequence | [`sequence-scan.png`](diagrams/sequence-scan.png) |
| Semantic stage + fusion | AI/ML pipeline | [`fusion.png`](diagrams/fusion.png) |
| Core model | UML class | [`class-core.png`](diagrams/class-core.png) |
| Report + ownership schema | ER / data model | [`data-model.png`](diagrams/data-model.png), landscape [`data-model-lr.png`](diagrams/data-model-lr.png) |
| Experiments | ML / evaluation workflow | [`evaluation-pipeline.png`](diagrams/evaluation-pipeline.png) |
| Schedule (current, baseline) | Gantt | [`gantt.png`](diagrams/gantt.png), [`gantt-baseline-2026-11-18.png`](diagrams/gantt-baseline-2026-11-18.png) |

## Mapping to the IPD brief

| Brief requirement | Slide(s) | Document |
|---|---|---|
| A Introduction, progress since the PP, scope changes | 1–4 | 03 (changes), 05 |
| B Stakeholders (onion), roles, influence | 5 | 02 |
| C FR/NFR, use case diagram, implemented / partial / pending, changes since the PP | 6–8, 4 | 03 |
| D Architecture, components, data flow, technologies, design artefacts, algorithms | 9–14, appendix A–C, E | 04 |
| E Updated Gantt, deviations, milestones | 18 | 05 |
| F Critical evaluation | 19 | 07 |
| Demo: repository, code walkthrough, completed / partial / not started, testing + results | 15–17, 20 | 06, 08 |
| Q&A: technical understanding, ownership | appendix | 09 |

## Rebuild after changes

```bash
ipd/diagrams/src/render.sh            # diagrams (needs node, mermaid-cli via npx, Chromium, python3)
python3 ipd/09-qa/make-class-guide.py # class guide from the code's doc comments
node ipd/01-slides/src/build-deck.js  # slide deck (see 01-slides/src/README.md)
```
