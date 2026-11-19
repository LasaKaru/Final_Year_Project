# ADR-007: Greedy decoding + JSON validation + evidence grounding + retry-once → else Unknown
Date: 2026-11-19 · Status: Accepted
## Context
Small local models are non-deterministic when sampling and often emit invalid or ungrounded JSON.
## Decision
`do_sample=false` (greedy), early stop after the first complete JSON object, strict schema validation, keep only evidence line IDs that exist in the slice (a non-unknown answer without grounded evidence becomes Unknown), one retry with "Return only the JSON object.", then Unknown with `parseOk=false`. Parse failures are counted in the coverage manifest.
## Consequences
+ Repeatability and failure transparency (NFR06/NFR07). − Greedy decoding may be less accurate than sampling + voting; to be compared post-IPD.
