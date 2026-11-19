# ADR-013: Endpoints only privileged roles can call are out of scope (SYM-10), per target
Date: 2026-11-19 · Status: Accepted

## Context
Scanning Oqtane (Dataset B-003) gave 52 Vulnerable endpoints, 19 of them restricted to `Host Users` or `Administrators`
(e.g. `DELETE api/alias/{id}`). Those roles administer every object by design: the attack in scope — one ordinary user
reaching a *peer's* object (horizontal BOLA) — cannot happen there. Counting them would bury real findings and make RQ2/RQ3
precision meaningless. Which roles are privileged is application knowledge, not something the analyser can infer.

## Decision
`analysis.privilegedRoles` (default empty). When every role in the endpoint's `[Authorize(Roles = …)]` is privileged and the
endpoint is not anonymous, the verdict is **OutOfScope, SYM-10** with the roles in the rationale. The list is set per target
(`eval/datasets/configs/oqtane.json`) and recorded with the run (config hash). Policies are not interpreted.

## Consequences
+ Report noise drops (Oqtane 52 → 33 Vulnerable) without hiding anything: SYM-10 rows stay in the report.
− Multi-tenant admins (site A admin vs site B) are not modelled; that is tenant isolation, a different property.
− A wrong list hides findings, so every list must be justified in the case manifest and is part of the labelled evidence.
