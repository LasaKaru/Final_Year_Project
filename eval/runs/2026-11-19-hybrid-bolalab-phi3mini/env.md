# Run environment — hybrid BolaLab, Phi-3-mini-4k-instruct (ONNX, CPU int4)

* Purpose: first hybrid run with a **capable** local model on the development set (BolaLab). Preliminary.
* Machine: cloud dev container — Ubuntu 24.04, x64, **4 logical cores**, no GPU; .NET 8.0.34; Release build.
  (Other builds were running concurrently, so latency is pessimistic.)
* Model: `microsoft/Phi-3-mini-4k-instruct-onnx`, folder `cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4` (2.6 GB,
  MIT licence, not committed). Prompt v1/phi3, JSON-schema guidance, greedy, max 160 new tokens.
* Fusion at the time of the run: escalation **on** (before ADR-012).
* Results: calibration gate **passed 3/4** (missed probe p2-recipe: protected by a LINQ owner predicate, answered vulnerable).
  11 SLM calls, 0 parse failures, latency 66–84 s per slice (median 78 s), model load 5.2 s, total 1165 s, peak 6.0 GB.
  Agreed with every symbolic Vulnerable; disputed TC09's delegated authorization (→ 1 FP) and escalated TC10 (labelled Unknown).
  With ADR-012 conservative fusion (default now) the final verdicts equal the symbolic ones: the model found no false
  positive to suppress because BolaLab's symbolic Vulnerables are all true positives.
* Comparison table: `metrics-compare.md` (this folder).
