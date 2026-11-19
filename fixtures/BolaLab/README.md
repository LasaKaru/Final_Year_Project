# BolaLab — deliberately vulnerable fixture API

> ⚠️ **Deliberately vulnerable. Never deploy it.**

BolaLab is a small ASP.NET Core + EF Core Web API with **one endpoint per test case (TC01–TC16)**.
It is BolaLens's regression suite, the development set for the symbolic rules and the IPD demo target.

* Expected labels live **only** in [`expected.json`](expected.json) — never in code comments (ADR-006).
* The comment inside `OrdersController.AdjustTotal` is an intentional *misleading-comment perturbation*, not a label.
* Results on BolaLab are development-set numbers and must be reported separately from datasets A/B/C.

Build: `dotnet build fixtures/BolaLab/BolaLab.sln`
