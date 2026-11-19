# Admission checklist (guide 07 §6)

| # | Evidence | A | B | C |
|---|---|---|---|---|
| 1 | Repository URL + immutable commit SHA | ✔ | ✔ | ✔ |
| 2 | Licence recorded and compatible with analysis/reporting | ✔ | ✔ | ✔ |
| 3 | Target framework, EF Core version, build command, build log | ✔ | ✔ | ✔ |
| 4 | Advisory ID + fix commit + vulnerable parent commit | ✔ | — | — |
| 5 | Intended policy documented (who may change what) | ✔ | ✔ | ✔ |
| 6 | Endpoint inventory created independently of the detector | ✔ | ✔ | ✔ |
| 7 | Secrets/personal-data check before storing any excerpt | ✔ | ✔ | ✔ |
| 8 | Repository family ID (for splitting) | ✔ | ✔ | ✔ |
| 9 | Decision: admitted / rejected / quarantined + reason | ✔ | ✔ | ✔ |

Do **not** copy third-party repositories into this repo — store URL + SHA and clone on demand.
Labels are human-made and frozen on 21 Mar 2027 (`eval/labels/<repo-id>.labels.json`, same format as `fixtures/BolaLab/expected.json`).
