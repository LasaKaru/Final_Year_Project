# B — Project stakeholders (IPD section B)

Diagram: [`../diagrams/stakeholder-onion.png`](../diagrams/stakeholder-onion.png) (onion model, 4 rings, colour = role type).

## Onion rings

| Ring | Stakeholders |
|---|---|
| 1. The system | **BolaLens** — CLI analyser, local small language model (SLM), local web UI, GitHub Action |
| 2. Containing system (operational) | Application developer (primary operator) · security reviewer / AppSec engineer · CI / DevOps pipeline owner · researcher-operator (the student) |
| 3. Containing business | Team lead / product owner · security & compliance officer · project supervisor (Ms. Pudara Dewdini) · IIT / University of Westminster examiners |
| 4. Wider environment | End users of the scanned APIs (data owners) · open-source maintainers of analysed projects · regulators / GDPR · research community · SAST/DAST vendors (CodeQL …) · model and framework providers (.NET, Roslyn, ONNX Runtime, Phi-3) · **attackers (negative stakeholder)** |

## Roles, interests, responsibilities, expectations

| Stakeholder | Role / responsibility | Interest and expectation | Requirements it drove |
|---|---|---|---|
| Application developer | Runs `bolalens scan` locally or in a pull request; fixes findings | Few false alarms; exact file and line; fast | FR13 source-anchored findings, NFR02 runtime, NFR04 explanations |
| Security reviewer | Triages findings, decides true/false positive | *Why* a path is flagged: the guard that was found and why it was rejected; Unknowns visible, never hidden | FR09 four-state verdict with rule IDs, FR16 call-graph view, NFR07 failure transparency |
| CI / DevOps owner | Integrates the tool in pipelines | Deterministic exit codes, SARIF for GitHub code scanning, no network | FR14 SARIF, FR15 Action + exit codes, NFR01 offline, NFR06 determinism |
| Researcher-operator (student) | Builds and evaluates the tool | Ablation switch, reproducible recorded runs, metrics | FR11 symbolic/hybrid modes, FR17 evaluation harness, NFR05 reproducibility |
| Team lead / product owner | Decides adoption | Low cost, source code never leaves the premises | NFR01 offline, NFR08 16 GB CPU laptop budget |
| Security & compliance officer | Data protection, licences | No code sent to third-party AI services; licences respected | NFR01, NFR11 tool safety, model licence recorded |
| Supervisor | Academic guidance, IPD examiner | Feasible scope, rigorous and honest evaluation, independent ownership | Scope narrowed to horizontal BOLA; CLI first; ADRs for every scope change; AI-use log |
| IIT / UoW examiners | Assessment and moderation | Working prototype, justified design, honest results | Requirement status tracking, live demo of real code, recorded runs |
| End users of scanned APIs | Indirect beneficiaries | Nobody else can read or change *their* records | Focus on object-level (horizontal) authorization, state-changing first, reads second (ADR-011) |
| OSS maintainers | Owners of the analysed repositories | Responsible disclosure, fair reporting | Only already-fixed advisories in Dataset A; no new issue is published without contacting the vendor |
| Research community | Readers, replicators | Benchmark, protocol, negative results reported | Pinned commits, draft → verified labels, CodeQL baseline arm, RQ2 negative result reported |
| SAST/DAST vendors | Competing / baseline tools | — | CodeQL C# `security-extended` used as the baseline arm (FR17) |
| Model / framework providers | Licensors | Licence compliance | Model manifest + SHA-256 in every report; Phi-3 (MIT), SmolLM2 (Apache-2.0) |
| Attackers (negative) | Could misuse findings | Exploitable leads | Defensive output only: no exploit generation; analysed code is never executed |

## How stakeholder input changed the project since the PP

| Input | Change |
|---|---|
| Supervisor / PP feedback + literature review | Narrowed the novelty claim; added the **Unknown** outcome (missing evidence is never "safe", ADR-003) |
| Developer / CI perspective | CLI + JSON + exit codes first (ADR-002); SARIF + a reusable GitHub Action instead of a heavy dashboard |
| Security reviewer perspective | Every finding shows the traced path, the guards found and *why* each was rejected; a local web UI with a call graph (ADR-010) |
| Compliance perspective | Strict offline proof: a full hybrid scan inside a network-less namespace (`unshare -n`), recorded |
| Real vendor advisories (OSS maintainers) | Most real BOLA CVEs are **reads** → read BOLA added as a secondary scope (ADR-011); membership/organisation checks (ADR-015) |
| Research community | Negative SLM result is reported, not hidden (conservative fusion, ADR-012; `eval/results/summary-hybrid.md`) |
