# Testing and preliminary results (24 Nov 2026)

All numbers come from recorded runs in `eval/runs/` (each with `env.md`: BolaLens commit, target commit, machine, time, memory).
Dataset labels are **drafts** derived from vendor fix commits and still need the owner's verification — every number below is
*preliminary*. BolaLab numbers are a **development set** and are never mixed with datasets A/B/C.

## 1. Automated tests

| Suite | Tests | What it covers |
|---|---|---|
| `BolaLens.Analysis.Tests` | 103 | every analyser pattern as an in-memory compilation (tests first); golden tests TC01–TC16 with expected rule IDs |
| `BolaLens.Inference.Tests` | 34 | parser, grounding, fusion decision table, schema guidance, model detection |
| `BolaLens.Integration.Tests` | 51 | full pipeline on BolaLab, SARIF schema, baseline SARIF, robustness, semantic-unit limiting |
| `BolaLens.Web.Tests` | 9 | web API end to end, no external resources, CSRF header, traversing run ids rejected |
| **Total** | **197** | all green; CI on every push (build, tests, Action self-test scan) |

## 2. Development set — BolaLab (16 hand-written cases)

| Arm | TP | FP | FN | TN | Precision | Recall | FPR | Time |
|---|---|---|---|---|---|---|---|---|
| Symbolic | 9 | 0 | 0 | 6 | 1.000 | 1.000 | 0.000 | 4.2 s, 209 MB |
| Hybrid SmolLM2-135M, gate failed 2/4 → advisory | 9 | 0 | 0 | 6 | 1.000 | 1.000 | 0.000 | 82 s |
| Hybrid SmolLM2-135M, no gate | 9 | 1 | 0 | 5 | 0.900 | 1.000 | 0.167 | 62 s |
| Hybrid Phi-3-mini, gate 3/4, *escalation allowed* (before ADR-012) | 9 | 1 | 0 | 5 | 0.900 | 1.000 | 0.167 | — |

TC10 (ambiguous DI) is correctly Unknown. The model rows motivated the calibration gate (ADR-009) and conservative fusion (ADR-012).

## 3. Real vulnerabilities — Dataset A (vulnerable commits) and C (their fixes)

14 published advisories in Oqtane (4), Kavita (3), Jellyfin (1), Bitwarden (5), IoTSharp (1); 33 labelled vulnerable units, 25
vulnerable → fixed pairs. Source: [`eval/results/summary-symbolic.md`](../../eval/results/summary-symbolic.md).

| Arm | Dataset A (vulnerable) | Dataset C (fixed) | Pairs told apart |
|---|---|---|---|
| **BolaLens symbolic** | TP 19 · FN 7 · Unknown 5 → **precision 1.000, recall 0.731**, operational recall 0.576 | TN 20 · FP 2 → **FPR 0.091** | **13 / 25** |
| CodeQL 2.27.5 `security-extended` (baseline) | TP 0 · FN 33 → recall 0.000 | TN 27 · FP 0 | 0 / 25 |
| BolaLens hybrid, Phi-3-mini (8 Oqtane/Kavita/IoTSharp cases) | TP 19 · FN 3 · Unknown 2 → precision 1.000, recall 0.864 | TN 16 · FP 2 → FPR 0.111 | = symbolic arm |

**Reading:** every endpoint BolaLens flagged in a vulnerable commit is a real (draft-labelled) BOLA; CodeQL's generic authorization
queries look for a missing `[Authorize]` on a whole action and found none of the object-level flaws. Misses are outside the
current ownership model: creation-time BOLA and organisation commands (Bitwarden), the Jellyfin library manager (not EF), Kavita
reads built outside EF, field-level exposure (Oqtane A-003). Error analysis: [`eval/results/error-analysis.md`](../../eval/results/error-analysis.md).

## 4. RQ2 — does a local SLM reduce false positives?

Phi-3-mini-4k int4, 15 hybrid reports, 29 SLM calls on labelled units ([`summary-hybrid.md`](../../eval/results/summary-hybrid.md)):

| Measure | Value |
|---|---|
| Final verdicts changed by the model | **0** (hybrid = symbolic arm) |
| Parse failures | 6 / 29 (symbolic verdict kept) |
| Model disagreed with the symbolic verdict | 9× — e.g. said Vulnerable on three `[ChapterAccess]`-protected Kavita downloads |
| Median latency / peak memory | 108 s per call / 8.9 GB |

**Preliminary answer: no.** On this data a 3.8B int4 model adds latency and a natural-language rationale, but no precision; the two
remaining Dataset C false positives were not recognised as safe by the model either. Reported as a negative result; Qwen2.5-Coder
comparison and τ calibration follow after the label freeze.

## 5. Wild repositories — Dataset B (unlabelled)

13 repositories (Conduit, CleanArchitecture, Oqtane 10.3, ErsatzTV, Jellyfin 10.11, Bitwarden server, …): **3,423 endpoints,
2,351 with id parameters**, scanned in 9 s – 6.4 min with 0.4 – 7.4 GB peak memory. 2,293 units are in blind labelling worksheets
(two raters); **no FP/FN claims are made before labelling**.

## 6. Robustness (FR21) — BolaLab

| Perturbation | Semantics preserved | Stability |
|---|---|---|
| P1 rename every local variable | yes | 1.000 |
| P2 misleading "ownership verified" comment before every statement | yes | 1.000 (comments never reach slices) |
| P3 rename every private method | yes | 1.000 |
| P4 rename owner properties (`UserId` → opaque) | no — stresses the naming heuristic | 0.313 |

P4 is an honest limitation: owner inference relies partly on names (mitigation: FK-to-user-type evidence, configuration).

## 7. Performance and resources (NFR02/NFR08)

| Target | Endpoints | Symbolic time | Peak memory |
|---|---|---|---|
| BolaLab | 16 | 4 s | 0.2 GB |
| Oqtane (3 projects) | 168–204 | 15–20 s | 0.6–0.7 GB |
| Kavita (.NET 10, C# 14 preview) | 505–543 | 1.1–1.5 min | 1.7–2.1 GB |
| Bitwarden server | 734–810 | 1.9–5.5 min | 3.7–5.4 GB |
| ErsatzTV | 113 | 4.9 min | 7.4 GB |
| Hybrid Phi-3-mini | per candidate | median 108 s | 8.9 GB |
