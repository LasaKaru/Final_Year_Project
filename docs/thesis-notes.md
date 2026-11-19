# Thesis notes (Implementation chapter seeds)

## Roslyn concepts (S0-06)
* **Syntax tree** — exact text structure incl. trivia (comments, whitespace); used for locations and slice text.
* **Compilation** — all trees + references of one project, type-checked; gives semantic models.
* **Semantic model** — answers what a piece of syntax *means* (which method a call binds to, the type of an expression).
* **Symbol** — a declared entity (type, method, property, parameter, local) with identity across files.
* **IOperation** — language-neutral tree of what the code *does*; the taint walker, guard detector and EF classifier work on it.

## Design notes
* Object identity across layers is modelled with entity tokens rather than symbols, because the same object has different
  symbols in controller, service and repository (ADR-008).
* Guard validity needs three bindings: trusted identity ↔ owner property ↔ same object, plus ordering. Each failure is kept
  as an *invalid guard* with a reason — these became the best explanations in the report.
* Bug found by unit tests: `ClaimTypes.NameIdentifier` contains "xmlsoap.org"; a substring test for "org" misclassified it
  as a tenant claim. Lesson: classify claim types by their last URI segment.
