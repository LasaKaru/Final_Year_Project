# Reproducibility

## Environment (development container, 2026-11-19)
* OS / CPU / RAM: Ubuntu 24.04.5 LTS, x64, 4 logical cores (fill in the laptop used for measurements)
* .NET SDK: 8.0.134 (`dotnet --info`), runtime 8.0.34; `global.json` pins 8.0.100 with `rollForward: latestFeature`
* Packages (central, `Directory.Packages.props`):

| Package | Version |
|---|---|
| Microsoft.CodeAnalysis.CSharp.Workspaces / Workspaces.MSBuild | 4.14.0 |
| Microsoft.Build.Locator | 1.11.2 |
| System.CommandLine | 2.0.15 |
| Spectre.Console | 0.57.2 |
| Microsoft.ML.OnnxRuntimeGenAI | 0.20.0 |
| xunit.v3 / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk | 3.2.2 / 3.1.5 / 17.14.1 |
| Shouldly | 4.3.0 |
| Microsoft.EntityFrameworkCore(.Relational) (tests) | 8.0.34 |
| BolaLab: Microsoft.EntityFrameworkCore.Sqlite, Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.34 |

* Models: SmolLM2-135M-Instruct int4 (built reproducibly by `scripts/get-model-smollm2.sh`; per-file SHA-256 in `models/README.md`);
  Phi-3-mini-4k-instruct cpu-int4 (pending) · each report stores `run.modelId` and a manifest hash
* Python tooling for model conversion only: onnxruntime-genai 0.20.0, torch 2.15.0+cpu, transformers 5.23.0, onnxruntime 1.32.0
* Prompt: `v1` — the report records `promptVersion` = `v1/<chat format> (<sha-256 prefix of format + template + few-shot examples>)`; JSON-schema guidance on; calibration gate result in `run.modelGate`
* Config: every report records `run.configHash` (SHA-256 prefix of the effective configuration)

## How to reproduce a run
```bash
git checkout <tag>
dotnet build BolaLens.sln -c Release
dotnet build fixtures/BolaLab/BolaLab.sln -c Release
dotnet run --project src/BolaLens.Cli -c Release -- scan <target> --mode symbolic|hybrid --out eval/runs/<id>/report.json
dotnet run --project src/BolaLens.Cli -c Release -- eval --report eval/runs/<id>/report.json --labels <labels.json> --out eval/runs/<id>/metrics.md
```
Measurement protocol (S4-05): record hardware in `eval/runs/<id>/env.md`; cold vs warm runs; three repetitions;
peak RAM from `run.peakWorkingSetBytes` and `/usr/bin/time -v` (Linux) or Task Manager (Windows).
