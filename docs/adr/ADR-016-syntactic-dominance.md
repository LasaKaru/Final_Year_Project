# ADR-016: Exiting checks must dominate the change (syntactic dominance, S6 step 1)
Date: 2026-11-23 · Status: Accepted (supersedes the "guard window" part of ADR-008 for exiting checks)

## Context
ADR-008's guard windows treat `if (mismatch) return Forbid();` as protecting everything after it in the method. That is
unsound when the check sits inside a branch or loop: `if (express) { if (o.UserId != uid) return Forbid(); } Save(o);` leaves
the non-express path unchecked. A full CFG is not needed for C#'s structured statements.

## Decision
For a check that leaves the method on failure (return/throw), the walker records its if-statement; every sink records, for
each active method frame, the statement through which it was reached. A covered sink only counts if the check **dominates**
that statement in the check's method: the check sits in a block that encloses the statement and precedes it, climbing through
`else` branches whose sibling always exits and through blocks that always run as a whole (try body, using, lock, nested
blocks). A statement inside one branch of the check itself is dominated when the other branch always exits
(`if (ok) { change } else { return Forbid(); }` — found on Oqtane `GET api/module/{id}`). Otherwise the check is rejected:
"check runs only on some paths to the state change". Region guards (change inside the then-branch) are already dominated by
construction.

## Consequences
+ Path-insensitivity false negatives of exiting checks are removed (9 tests, BolaLab unchanged).
+ Re-recorded datasets (2026-11-23): Dataset A/C metrics unchanged; after the branch rule no real case has a dominance
  rejection — real controllers put checks first, so the change matters for robustness (P-perturbations), not for these numbers.
− `goto`, labels and exceptions thrown between check and change are not modelled; a Roslyn `ControlFlowGraph` remains the
  option if datasets show such code.
