# IPD submission checklist

Deadline: **Thu 28 Jan 2027 (IIT)**. Live demo + Q&A: 29 Jan – 19 Feb 2027 (Teams, recorded). Tick each item.

## What to upload (confirm on Blackboard — record the answer in `submission-requirements.md`)
- [ ] Slide deck `ipd/01-slides/BolaLens-IPD.pptx` (+ PDF) — **rewritten in your own words**
- [ ] GitHub repository link (branch `dev`, tag `v0.1-ipd`)
- [ ] Anything else Blackboard asks for (report? video?) — confirm with the supervisor

## Before the code freeze (20 Jan 2027)
- [ ] Ethics Part A signed (overdue — planned 27 Nov)
- [ ] Supervisor reviewed ADR-011 … ADR-016 (scope changes)
- [ ] Draft labels of Datasets A/C verified by you (`eval/labels/drafts`) — re-run `python3 scripts/dataset-report.py`
- [ ] All recorded runs regenerated from one commit; numbers on slides 3, 15, 16, 17 and in `06-results` updated
- [ ] `CLAUDE.md` status and `docs/requirements.md` updated; `ipd/diagrams/src/gantt.mmd` "today" updated and re-rendered
- [ ] Tag: `git tag v0.1-ipd && git push origin v0.1-ipd`

## Slides and talk
- [ ] Every slide's text and speaker notes rewritten in your own words; your name and IDs correct on slide 1
- [ ] Research questions on the slides use the exact PP wording
- [ ] Rehearsal 1 (timed: slides ≤ 12 min) · Rehearsal 2 (with demo ≤ 20 min) · Rehearsal 3 (in front of someone, with questions)
- [ ] Appendix slides ready for the Q&A

## Demo
- [ ] `ipd/08-demo/demo-prep.sh` passes on the presentation laptop (Windows or Linux) the day before
- [ ] `../datasets/A-001-vuln` and `A-001-fixed` cloned on that laptop
- [ ] Backup video recorded (`ipd/08-demo/backup-video-plan.md`) — location: ______________________
- [ ] Teams screen sharing tested; notifications off; terminal font ≥ 16 pt

## Q&A
- [ ] Every row of `ipd/09-qa/class-guide.md` ticked (you can explain purpose · input · limitation)
- [ ] Answers to `ipd/09-qa/qa-preparation.md` practised **in your own words**
- [ ] `docs/ai-log.md` up to date and honest
