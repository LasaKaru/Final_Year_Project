# Run environment — 2026-11-19 symbolic BolaLab

* Purpose: first preliminary run of the symbolic-only arm on the **development set** (BolaLab). Not a research result.
* Machine: cloud dev container — Ubuntu 24.04.5 LTS, x64, 4 logical cores (see `report.json` → `run.machine`)
* .NET SDK 8.0.134, runtime 8.0.34; Release build
* Command:
  `dotnet run --project src/BolaLens.Cli -c Release -- scan fixtures/BolaLab/BolaLab.sln --mode symbolic --out eval/runs/2026-11-19-symbolic-bolalab/report.json`
* Total 4.2 s (load 1.9 s, compile 2.5 s, paths 0.06 s); peak working set 209 MB
* Hybrid arm: **not run** — the model could not be downloaded in this environment. Re-run on the 16 GB laptop with
  `--mode hybrid` and three repetitions (S4-05).
