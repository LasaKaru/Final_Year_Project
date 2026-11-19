# ADR-006: Expected labels live in expected.json, never in code comments
Date: 2026-11-19 · Status: Accepted
## Context
Comments or identifiers that reveal the expected answer would leak into the SLM prompt and inflate results.
## Decision
Labels only in `fixtures/BolaLab/expected.json` (and later `eval/labels/*.labels.json`). The slicer renders code from tokens, dropping all comments, and shortens long string literals. Few-shot examples come only from `fixtures/Calibration` (different entity names). Tests assert that slices contain no rule IDs, test-case IDs or comments, and that few-shot text does not mention BolaLab entities.
## Consequences
+ Prevents label leakage. − The misleading-comment perturbation (TC16) is invisible to the v1 slicer; the post-IPD robustness experiment keeps comments on purpose.
