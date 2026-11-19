# Live demo script — current build (target 8 min; run from tag `v0.1-ipd` on the laptop)

Complements `docs/plan/09_IPD_PRESENTATION_AND_DEMO_GUIDE.md` §4 (baseline). Everything runs offline; Wi-Fi can be off.

## 30 minutes before
```bash
git checkout v0.1-ipd && git status            # clean tree
dotnet build BolaLens.sln -c Release && dotnet build fixtures/BolaLab/BolaLab.sln -c Release
dotnet test BolaLens.sln -c Release --no-build --filter "Category!=RequiresModel"   # all green, note the count
scripts/ensure-model.sh                         # committed SmolLM2 checksums OK
scripts/run-case.sh A-001                       # clones Oqtane once (needs network) → do it the day before
dotnet run --project src/BolaLens.Web -c Release   # http://127.0.0.1:5057, keep the tab open
```
Open in tabs: the repo on GitHub (Actions tab, last green run), `fixtures/BolaLab/src/BolaLab.Api/Controllers/OrdersController.cs`,
`../datasets/A-001-vuln/Oqtane.Server/Controllers/NotificationController.cs`, `docs/diagrams/stakeholder-onion.png`. Backup video ready.

## Script

| Min | Show | Say (in your own words) | Fallback |
|---|---|---|---|
| 0:00 | Repo tree + green CI (tests, Action self-test) | Layered pipeline, 9 projects, ~160 tests, CI scans BolaLab with the Action | screenshot `docs/ipd/screenshots` |
| 0:45 | `OrdersController.cs` TC01 vs TC02 | Same update, one owner check — the bug class (horizontal, state-changing BOLA) | — |
| 1:30 | Terminal: `dotnet run --project src/BolaLens.Cli -c Release -- scan fixtures/BolaLab/BolaLab.sln --quiet` | Roslyn loads the solution; table of verdicts with rule IDs; exit code 1 = CI gate | `eval/runs/2026-11-19-symbolic-bolalab/console.txt` |
| 2:30 | Web UI → new scan (symbolic) → TC01 finding | Call graph controller → service → repository → red sink; "Checks found" with the reason each was rejected | `docs/ipd/screenshots/call-graph-desktop.png` |
| 3:30 | TC16 (protected, renamed caller variable) → green sink | Guard binding: caller identity ↔ owner property ↔ same object, before the change | `call-graph-phone.png` |
| 4:15 | TC10 → Unknown | Ambiguous DI dispatch → honest Unknown, never "safe" (ADR-003) | — |
| 4:45 | Real CVE: Oqtane notification PUT, `A-001-vuln` vs `A-001-fixed` (`eval/runs/2026-11-19-symbolic-A-001-*/metrics.md`) | Vendor fixed it on 31 Jul 2026; BolaLens flags the vulnerable commit ("owner value comes from the request body") and passes the fix | `eval/results/summary-symbolic.md` |
| 5:45 | Hybrid: `scan … --mode hybrid` (SmolLM2) or the recorded Phi-3 run | Slice is label-free; JSON-schema-guided answer; calibration gate; conservative fusion — the SLM may only suppress | `eval/runs/2026-11-19-hybrid-bolalab-phi3mini/metrics-compare.md` |
| 6:45 | `eval --report … --labels fixtures/BolaLab/expected.json` | Confusion matrix with Unknown kept separate; BolaLab = development set only | `metrics.md` |
| 7:15 | `robustness fixtures/BolaLab/BolaLab.sln -p P1 P2` | Renamed locals / misleading comments: stability 1.000; comments never reach the slice | `eval/runs/2026-11-19-robustness-bolalab/robustness.md` |
| 7:45 | Close: `scan --sarif` → open SARIF; README Action snippet | Integrates with GitHub code scanning | — |

## Rules
* Narrate what the code does, not what the slide says. Be ready to open any class the examiner names (R6).
* If a command hangs > 20 s, switch to the recorded run in `eval/runs` and say so.
* Never claim BolaLab numbers as generalisation; point to Dataset A pairs (3/6 discriminated, draft labels) as the honest preliminary result.
