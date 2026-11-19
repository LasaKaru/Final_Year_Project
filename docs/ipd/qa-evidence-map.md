# Q&A evidence map (where the proof is — prepare the spoken answers yourself)

| Likely question | Evidence to open |
|---|---|
| How does a route id become "tracked" and reach EF Core? | `src/BolaLens.Analysis/Taint/PathWalker.cs` (seeding, `EvalInvocation`, `TokenFrom`), golden TC13/TC14 |
| What exactly counts as a valid owner check? | `PathExtractor.StaticReason`, `PathWalker.BuildGuard/BuildHelperGuard/AcceptOwnerDisjunctions`, `RequestEntityPatternsTests` |
| Why not a CFG / dataflow framework? | ADR-008 (entity tokens + guard windows), known-limitations §path-insensitivity, swallowed-throw fix, S6 plan |
| How do you avoid label leakage into the model? | ADR-004/006, `IrSliceGenerator` (no verdicts/rule IDs), P2 robustness (comments never reach slices), few-shot only from `fixtures/Calibration` |
| Why can the SLM only suppress? | ADR-012 + `eval/runs/*phi3mini*` (escalation produced the only errors) |
| Is the tiny model useful at all? | No — gate 2/4 → advisory (ADR-009); it proves the offline pipeline; Phi-3 is the research model |
| How do you know the results are not overfitted to BolaLab? | BolaLab = development set (README); Dataset A pairs + Dataset C controls (`eval/results/summary-symbolic.md`), error analysis (`eval/results/error-analysis.md`) |
| Where do the labels come from? | `eval/labels/drafts/README.md` (vendor fixes, to be verified), blind worksheets protocol (`eval/labels/worksheets/README.md`), freeze 21 Mar |
| What is the baseline? | CodeQL C# security-extended (`.github/workflows/baseline-codeql.yml`), `BaselineSarif` adapter |
| What about read-only IDOR (most CVEs)? | ADR-011 read mode; A-002 GET discriminated; A-003 field-level miss explained |
| Performance? | `eval/results/summary-symbolic.md` (Oqtane 15–19 s, ~600 MB); Phi-3 78 s/slice |
| Why .NET 8 after its end of support? | ADR-001; one-line switch to .NET 10 (README) |
| How did you use AI? | `docs/ai-log.md` — and show that you can explain any class |
| Biggest weaknesses? | `docs/known-limitations.md` (naming heuristics: P4 stability 0.313; custom permission services; field-level exposure) |
