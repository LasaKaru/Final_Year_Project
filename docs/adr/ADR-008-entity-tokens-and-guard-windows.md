# ADR-008: Object identity via entity tokens; guard protection windows in execution order
Date: 2026-11-19 · Status: Accepted (replaced by CFG dominance in S6)
## Context
A guard only protects a change if it concerns the *same object* (TC04) and happens *before* the change (TC05), possibly in another method (TC14, controller → service → repository).
## Decision
Each EF Core load / detached construction / bulk query creates an `EntityToken`; the token (reference identity) flows through locals, arguments, return values and `this`, across methods. The walker visits code in execution order with a global sequence number. An exit guard (`if (mismatch) return/throw`) is active from the end of the `if` until the end of its method (`return`) or for the rest of the path (`throw`); a branch guard (`if (match) { … }`) covers only that branch. A sink is protected iff a valid guard on the same token covers its sequence number.
## Consequences
+ Explains TC03/TC04/TC05/TC14 precisely and generalises to helper methods that throw. − Path-insensitive: loops and complex branches are approximated; summaries are recomputed per call site (depth ≤ 5). S6 replaces windows with `ControlFlowGraph` dominance.
