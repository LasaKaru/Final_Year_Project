# ADR-011: Read BOLA (IDOR) as a secondary, opt-in scope
Date: 2026-11-19 · Status: Accepted

## Context
The proposal limits the scope to *state-changing* horizontal BOLA. A systematic search of all 2,014 NuGet-ecosystem advisories
in OSV (filter: CWE-639/284/285/862/863/566/200/269/932 + IDOR keywords; `eval/datasets/search-log.md`) found that the .NET
application advisories with code are mostly **read** IDORs (e.g. Oqtane CVE-2024-55186 notification read, CVE-2024-55471 user
profile read, FormCMS CVE-2025-55797). With state-changing scope only, Dataset A would be almost empty.

## Decision
Keep state-changing BOLA as the primary scope and the headline metric. Add `analysis.includeReadOnlyPaths` (default false):
an object loaded by a client-controlled selector becomes a **Read** sink; read sinks are protected by any executed owner check
on the same object, an owner predicate or an applicable query filter. Read-mode results are reported as a separate table.

## Consequences
+ Historical cases can be evaluated (RQ1) without changing the primary claim. − Field-level exposure (an endpoint that returns
an object but filters private fields, CVE-2024-55471) is not modelled; such cases are expected misses and listed as such.
