# Run environment — 2026-11-19 hybrid BolaLab, SmolLM2-135M-Instruct int4 (nogate)

* Purpose: first real hybrid runs on the **development set** (BolaLab). Preliminary; not a research result.
* Machine: cloud dev container — Ubuntu 24.04.5 LTS, x64, 4 logical cores; .NET SDK 8.0.134 / runtime 8.0.34; Release build.
* Model: `models/smollm2-135m-instruct-int4` (86 MB, built by `scripts/get-model-smollm2.sh`; hashes in models/README.md),
  ChatML prompt v1, JSON-schema guidance on, greedy decoding, max 160 new tokens.
* Variant: **nogate** — gated = calibration gate on (default); nogate = `--no-gate`; offline = gate on, run inside
  `unshare -n` (no network interfaces) as NFR01 evidence.
* Command: `bolalens scan fixtures/BolaLab/BolaLab.sln --mode hybrid --model models/smollm2-135m-instruct-int4 --no-gate`
* Observations: model answers "vulnerable" (0.90) for every slice → gate 2/4 → advisory only (gated/offline). Without the gate
  it disputes TC09 (FP) and escalates TC10. ~5 s per slice, model load 0.37 s, peak working set ~1 GB.
  Gated and offline runs produced byte-identical model outputs for all 18 findings (determinism, NFR06).
* Comparison table: `eval/runs/2026-11-19-hybrid-bolalab-smollm2-nogate/metrics-compare.md`.
