# BolaLens — Automated Context-Aware BOLA Detection in ASP.NET Core Web APIs

[![ci](https://github.com/LasaKaru/Final_Year_Project/actions/workflows/ci.yml/badge.svg?branch=dev)](https://github.com/LasaKaru/Final_Year_Project/actions/workflows/ci.yml)

Final-year project (IIT 20223087 | UoW W19598693) · Supervisor: Ms. Pudara Dewdini

BolaLens is an **offline**, three-stage static analyser that finds **horizontal, state-changing Broken Object Level
Authorization (BOLA)** in ASP.NET Core + EF Core Web APIs:

1. **Roslyn symbolic engine** — endpoint inventory, global policy map, EF Core ownership schema, DI resolution and an
   inter-procedural taint walker that follows a client-controlled route/body id through controller → service → repository
   to EF Core updates/deletes, checks guards, and gives a three-state verdict (Vulnerable / Protected / Unknown) with rule IDs.
2. **Local SLM validation** — a label-free IR slice of each candidate path is judged by a local ONNX model
   (Phi-3-mini via ONNX Runtime GenAI); its grounded JSON verdict is fused with the symbolic verdict.
3. **Reporting** — `report.json` (findings, spans, reasons, coverage, run metadata), console summary, CI exit codes,
   and an evaluation harness (confusion matrix, precision/recall/F1/FPR/FDR).

> Built on **.NET 8** (`net8.0`, ADR-001). .NET 8 reached end of support on 10 Nov 2026; switching to .NET 10 is one line in
> `Directory.Build.props` plus `global.json`.

## Quick start

```bash
dotnet build BolaLens.sln -c Release
dotnet build fixtures/BolaLab/BolaLab.sln          # the analysed target must be restored
dotnet run --project src/BolaLens.Cli -c Release -- inventory fixtures/BolaLab/BolaLab.sln
dotnet run --project src/BolaLens.Cli -c Release -- scan fixtures/BolaLab/BolaLab.sln --mode symbolic --out report.json
dotnet run --project src/BolaLens.Cli -c Release -- eval --report report.json --labels fixtures/BolaLab/expected.json
dotnet test BolaLens.sln --filter "Category!=RequiresModel"
```

Hybrid mode uses a local model. The tiny one (SmolLM2-135M int4, 86 MB) is **committed** in `models/` and is the default, so
after cloning it works offline straight away (`scripts/ensure-model.sh` / `.ps1` verifies it and rebuilds it if missing).
Bigger models (Phi-3-mini) are passed with `--model`; see [`models/README.md`](models/README.md).

```bash
dotnet run --project src/BolaLens.Cli -c Release -- scan fixtures/BolaLab/BolaLab.sln --mode hybrid
```

### Web UI (local, responsive, offline)

```bash
dotnet run --project src/BolaLens.Web        # → http://127.0.0.1:5057
```
Start scans (symbolic or hybrid, model picker, calibration gate), browse runs, and inspect every finding: the traced path
controller → service → repository, the checks found (with the reason each one was rejected), EF Core sinks, the label-free
IR slice with the model's evidence lines highlighted, endpoints, policy map, ownership schema, DI map, per-phase timings —
and evaluate a run against labels. Works on desktop and phones (light/dark). Any run can be downloaded as a single
**offline HTML report** (also `bolalens html --report r.json --out report.html`). Screenshots: [`docs/ipd/screenshots`](docs/ipd/screenshots).

## Commands

| Command | Purpose |
|---|---|
| `inventory <path>` | Load a .sln/.slnx/.csproj; list projects, TFMs, documents, compile errors, workspace diagnostics |
| `endpoints <path> [--out]` | Endpoint inventory JSON (verb, route, parameters + binding, id-like, `[Authorize]`) |
| `policy <path> [--out]` | Global authentication/authorization policy map JSON |
| `schema <path> [--out]` | EF Core ownership schema JSON (entities, keys, owner candidates, query filters) |
| `di <path> [--out]` | DI registration map (service → implementations) |
| `paths <path> [--endpoint "PUT api/orders/{id}"] [--out]` | Annotated execution paths + symbolic verdicts |
| `slice <path> --endpoint … [--prompt]` | The label-free IR slice (and the full prompt) |
| `scan <path> --mode symbolic\|hybrid [--config] [--out] [--model] [--save dir] [--no-gate]` | Full pipeline → `report.json` + console summary |
| `eval --report r.json --labels l.json [--compare r2.json …] [--out metrics.md]` | Metrics; a hybrid report gives both arms (ablation) |
| `models [--dir models]` | List local ONNX models (size, family, chat format) |
| `html --report r.json [--context c.json] --out report.html` | Self-contained offline HTML report |
| `sarif --report r.json --out r.sarif [--uri-prefix dir] [--sarif-no-unknown]` | SARIF 2.1.0 for GitHub code scanning (also `scan --sarif`) |

Exit codes: `0` no Vulnerable findings · `1` Vulnerable findings present (CI gating; `--no-fail` disables) · `2` load/usage error.
All heuristics (identity sources, owner-name patterns, depth, model settings) are in [`bolalens.json`](bolalens.json) (`--config`).

### GitHub Action (CI gate + code scanning)

```yaml
jobs:
  bola:
    runs-on: ubuntu-latest
    permissions: { contents: read, security-events: write }
    steps:
      - uses: actions/checkout@v4
      - uses: LasaKaru/Final_Year_Project@dev          # pin a tag/SHA in real use
        with:
          solution: src/MyApi.sln
          target-dotnet-version: 10.0.x
          upload-sarif: 'true'                         # alerts in Security → Code scanning
          fail-on-findings: 'true'                     # exit 1 when an endpoint is Vulnerable
```
SARIF output is validated against the OASIS 2.1.0 schema; Vulnerable = `error`, Unknown = `note` (kind `review`), the traced
controller → service → repository path is a code flow. The action never runs the scanned application.

## Symbolic rules (first match per sink; a path's verdict is its worst sink)

| Order | Rule | Condition | Verdict |
|---|---|---|---|
| 1 | SYM-01 | no state-changing sink, tracked id not lost in unresolved code | OutOfScope |
| 2 | SYM-02 | no state-changing sink, but the id entered unresolved/ambiguous/too-deep code | Unknown |
| 3 | SYM-03 | valid owner equality or LINQ owner predicate (caller identity ↔ owner property ↔ same object, before the change) | Protected |
| 4 | SYM-05 | resource-based `AuthorizeAsync(User, entity, …)` gates the change | Protected (delegated → SLM candidate) |
| 5 | SYM-06 | loaded with `IgnoreQueryFilters()` and no valid guard | Vulnerable |
| 6 | SYM-07 | detached write from a client key, or bulk write without owner predicate/filter | Vulnerable |
| 7 | SYM-04 | applicable query filter at the required owner level | Protected |
| 8 | SYM-09 | changed entity has no owner candidate | Unknown |
| 0 | SYM-10 | only configured privileged roles (`analysis.privilegedRoles`, empty by default) can call the endpoint | OutOfScope |
| 9 | SYM-08 | owned entity selected by client id is changed with no valid guard or filter | Vulnerable |

## Preliminary results (development set — not a research result)

`fixtures/BolaLab` is a deliberately vulnerable API with 16 labelled cases (TC01–TC16, labels only in
[`expected.json`](fixtures/BolaLab/expected.json)). Runs of 2026-11-19 ([table](eval/runs/2026-11-19-hybrid-bolalab-smollm2-nogate/metrics-compare.md)):

| Arm | TP | FP | FN | TN | Precision | Recall | FPR | Time | Peak RAM |
|---|---|---|---|---|---|---|---|---|---|
| Symbolic (Roslyn only) | 9 | 0 | 0 | 6 | 1.000 | 1.000 | 0.000 | 4.2 s | 209 MB |
| Hybrid, SmolLM2-135M int4, calibration gate (failed 2/4 → advisory) | 9 | 0 | 0 | 6 | 1.000 | 1.000 | 0.000 | 82 s | ~1 GB |
| Hybrid, SmolLM2-135M int4, **no gate** | 9 | 1 | 0 | 5 | 0.900 | 1.000 | 0.167 | 62 s | ~1 GB |

TC10 (ambiguous DI dispatch) is correctly **Unknown** in the symbolic arm. The 135M model answers "vulnerable" for every
slice; without the calibration gate (ADR-009) it overrides TC09's delegated authorization (a new false positive) — with the
gate, a weak model can never make the result worse. Two runs gave byte-identical model outputs (greedy decoding), and a
hybrid scan ran to completion with **no network interfaces** (`unshare -n`). Because the rules were developed on BolaLab,
these numbers show correctness on the fixtures only; generalisation is measured on datasets A/B/C with a capable model.

## Architecture

Pipeline (pipes-and-filters) inside a layered solution. `Core` has no Roslyn types; only `Cli` wires things together;
`Inference` never sees Roslyn (it gets an `IrSlice`). Only `Cli` and `Web` are composition roots. Swapping `OnnxSemanticValidator` for `NullSemanticValidator` is the ablation.

```mermaid
flowchart LR
    Cli --> App
    Web --> App
    App --> Analysis & Slicing & Inference & Reporting & Eval
    Analysis --> Core
    Slicing --> Core
    Inference --> Core
    Reporting --> Core
    Eval --> Core
```

| Project | Responsibility |
|---|---|
| `src/BolaLens.Core` | Records (endpoints, policy, schema, paths, sinks, guards, verdicts, report), config, JSON options |
| `src/BolaLens.Analysis` | Stage 1: loading, endpoints, policy, schema, DI, taint walker, EF classifier, guards, rules, pipeline |
| `src/BolaLens.Slicing` | Stage 2a: IR slices, prompt v1 (embedded), few-shot examples from `fixtures/Calibration` |
| `src/BolaLens.Inference` | Stage 2b: ONNX Runtime GenAI generator/validator, response parser, fusion policy |
| `src/BolaLens.Reporting` | Stage 3: JSON report, console summary, finding IDs, coverage manifest |
| `src/BolaLens.Eval` | Labels, unit matching, confusion matrix, metrics, Markdown tables |
| `src/BolaLens.App` | Application services shared by both front ends: `ScanService` (+ calibration gate), `RunStore` |
| `src/BolaLens.Cli` | `bolalens` commands (composition root) |
| `src/BolaLens.Web` | Local responsive web UI `bolalens-ui` (minimal API + vanilla JS, localhost only) |
| `tests/*` | Unit tests on in-memory compilations, BolaLab golden/integration tests, inference/eval tests |
| `fixtures/BolaLab`, `fixtures/Calibration` | Test API (own solution, never deployed); calibration code for few-shot examples and gate probes |
| `models/` | Committed tiny model `smollm2-135m-instruct-int4` (+ `.sha256` manifest); other models git-ignored |
| `scripts/` | `ensure-model.sh/.ps1` (verify or rebuild the tiny model), `get-model-smollm2.sh/.ps1` (reproducible build) |
| `eval/` | Dataset registry/search log, frozen labels, splits, recorded runs |
| `docs/` | ADRs, requirements status, diagrams, known limitations, reproducibility, logs |

More: [CLAUDE.md](CLAUDE.md) (project memory, status, next steps) · [plan pack](docs/plan) · [IPD brief](docs/ipd/ipd-brief.md) · [ADRs](docs/adr) · [requirements status](docs/requirements.md) · [known limitations](docs/known-limitations.md) ·
[reproducibility](docs/reproducibility.md) · [diagrams](docs/diagrams) · [AI-use log](docs/ai-log.md).

## Safety

BolaLens only parses and type-checks the analysed code — it never executes it — and runs fully offline (no network code in
`src/`; the model runs locally with no tools). BolaLab is deliberately vulnerable: never deploy it.
