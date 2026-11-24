# Live prototype demonstration — script (target 8 minutes, inside the 20-minute slot)

Runs from tag `v0.1-ipd` on the 16 GB laptop, **fully offline** (Wi-Fi can be off). Preparation: [`demo-prep.sh`](demo-prep.sh).
Fallback for every step: the recorded runs in `eval/runs/` and the screenshots in [`../screenshots`](../screenshots); backup video:
[`backup-video-plan.md`](backup-video-plan.md). Speak in your own words — narrate what the *code* does, not what the slide says.

## The day before
1. `git checkout v0.1-ipd`, then run `ipd/08-demo/demo-prep.sh` — builds, runs all tests, checks the model, and scans BolaLab once.
2. Make sure `../datasets/A-001-vuln` and `../datasets/A-001-fixed` exist (Oqtane clones; `scripts/run-case.sh A-001` needs network once).
3. Record the backup video (see plan) and copy it to two places.

## 30 minutes before
* Start the UI: `dotnet run --project src/BolaLens.Web -c Release` → open http://127.0.0.1:5057.
* Open tabs: GitHub repository (Code + Actions tab with the last green run), VS Code on the repository with
  `fixtures/BolaLab/src/BolaLab.Api/Controllers/OrdersController.cs`, `src/BolaLens.Analysis/Taint/PathWalker.cs`,
  `src/BolaLens.Analysis/Guards/Dominance.cs`, `src/BolaLens.Inference/FusionPolicy.cs`, and `eval/results/summary-symbolic.md`.
* Terminal font ≥ 16 pt; close notifications; Teams screen share on the whole desktop.

## Script

| Min | Show | Say (key points) | Status shown | Fallback |
|---|---|---|---|---|
| 0:00 | GitHub repo: `src/` (9 projects), `tests/`, `eval/`, `docs/adr`, Actions tab green | Layered pipeline; 197 tests; CI scans BolaLab with our own Action on every push | completed | `ipd/screenshots` |
| 0:45 | `OrdersController.cs`: TC01 `PUT …/status` vs TC02 `PUT {id}` | Same update; TC02 checks `order.UserId == caller` first. That one missing line is BOLA | — | — |
| 1:30 | Terminal: `dotnet run --project src/BolaLens.Cli -c Release -- scan fixtures/BolaLab/BolaLab.sln` | Roslyn loads the solution; verdict table with rule IDs; exit code 1 = CI gate | completed | `eval/runs/2026-11-19-symbolic-bolalab/console.txt` |
| 2:30 | Web UI → new symbolic scan → TC01 finding → call graph | Path controller → service → repository → red sink; "Checks found" lists each rejected check *with the reason* | completed | `call-graph-desktop.png` |
| 3:15 | TC16 (protected, caller variable renamed) and TC10 (Unknown) | Guard binding = same object + caller identity + dominates the change; ambiguous DI → honest Unknown | completed | `d3-finding.png` |
| 4:00 | Code walkthrough: `PathWalker` (seeding, entity token) → `GuardDetector` → `Dominance.Dominates` | Explain the algorithm on the real code (be ready for any class) | completed | — |
| 5:00 | Real CVE: Oqtane A-001 `PUT api/notification/{id}` — `eval/runs/2026-11-23-symbolic-A-001-vuln` vs `-fixed` | Flagged in the vulnerable commit (owner value from the request body), passes after the vendor fix | completed | `summary-symbolic.md` |
| 5:45 | `eval/results/summary-symbolic.md` metrics + CodeQL row | Draft labels: precision 1.000, recall 0.731, 13/25 pairs; CodeQL 0/33 | in progress (labels draft) | — |
| 6:30 | Hybrid: `scan … --mode hybrid` (SmolLM2, ~1 min) **or** recorded Phi-3 run + `FusionPolicy.cs` | Label-free slice; JSON-schema answer; gate; the SLM may only suppress — and on real data it changed nothing (RQ2) | partial (τ uncalibrated) | `eval/runs/2026-11-23-hybrid-A-005-vuln` |
| 7:15 | `robustness fixtures/BolaLab/BolaLab.sln -p P1 P2` | Renames/comments → stability 1.000; P4 0.313 = known limitation | completed | `eval/runs/2026-11-19-robustness-bolalab` |
| 7:45 | Close: what is *not* started — calibration of τ, model comparison, tenant scoping, Windows run | Point to the Gantt and the critical evaluation | not started | slide |

## Rules
* If a command takes more than 20 s, say so and switch to the recorded run — never wait in silence.
* Never present BolaLab numbers as generalisation; Dataset A/C are the real (preliminary) results.
* If asked to open a class you cannot explain — say which part you would check, then open its doc comment (purpose · input · limitation).
