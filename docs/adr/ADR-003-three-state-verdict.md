# ADR-003: Three-state verdict Vulnerable / Protected / Unknown (+ OutOfScope)
Date: 2026-11-19 · Status: Accepted
## Context
The October literature review showed that unresolved evidence (ambiguous DI dispatch, external calls, depth limits) must not silently become "safe".
## Decision
Every path gets Vulnerable, Protected, Unknown or OutOfScope (no state change). Unknown carries the reason (rule SYM-02/SYM-09). Unknowns are counted in the coverage manifest and reported separately in the metrics (operational recall counts them as misses).
## Consequences
+ Honest recall/coverage; examiners can see where the tool gave up. − More verdict categories to explain.
