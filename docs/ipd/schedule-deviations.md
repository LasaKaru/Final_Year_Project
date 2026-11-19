# Schedule deviations (for the IPD "updated Gantt" slide)

Baseline: `docs/diagrams/gantt-baseline-2026-11-18.mmd` (the PP timeline is superseded). Current: `docs/diagrams/gantt.mmd`.

| Item | Planned | Actual / new | Deviation and reason |
|---|---|---|---|
| Symbolic engine S1–S3 | 30 Nov – 10 Jan | done 19 Nov | **Earlier**: AI-assisted implementation (logged in `docs/ai-log.md`); the time saved is re-planned into owner verification (risk R6) and datasets |
| IR slices + local SLM S3b–S4 | 1–20 Jan | done 19 Nov (SmolLM2 + Phi-3-mini runs) | **Earlier**; Phi-3 runs at ~78 s/slice on 4 cores → hybrid is for candidates only |
| Web UI | Could (M4, April) | done (ADR-010) | **Scope added**: PP promised a dashboard with call graphs (RQ4); built as a local, offline UI instead of React |
| SARIF / GitHub Action / robustness | April (M4) | done 19 Nov | **Earlier**; CI self-tests the Action |
| Datasets A/B/C | Dec – 21 Mar | framework + 4 A-cases, 3 B-repos recorded | **On track**; labels are drafts/blind worksheets until the owner verifies them |
| Scope | state-changing BOLA only | + read BOLA (secondary, ADR-011), SYM-10 privileged roles (ADR-013), conservative fusion (ADR-012) | **Changed** after real advisories turned out mostly read-only and Phi-3 only added FPs through escalation |
| Baseline SAST tool | "to choose" | CodeQL C# (security-extended) via a manual workflow + SARIF adapter | **Decided**; local download blocked in the cloud container, so it runs in GitHub Actions |
| CFG dominance (S6) | Feb – Mar | still planned; first soundness fix (swallowed throws) done | **Unchanged** |
| Ethics Part A | by 27 Nov | open | **Critical** — owner action |
