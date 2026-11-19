# Robustness — fixtures/BolaLab/BolaLab.sln

Units = endpoints with at least one traced path; a unit is unchanged when its worst symbolic verdict is the same.
Semantics-preserving perturbations should give stability 1.000; P4 measures dependence on owner-name heuristics.

| Perturbation | Preserves semantics | Units | Unchanged | Stability | Compile errors before → after | Identical slices (line numbers ignored) | Time |
|---|---|---|---|---|---|---|---|
| P1-rename-locals — every local variable renamed to an opaque name (v1, v2, …) | yes | 16 | 16 | 1.000 | 0 → 0 | 2/18 | 0.8 s |
| P2-misleading-comments — a comment claiming the ownership was verified before every statement | yes | 16 | 16 | 1.000 | 0 → 0 | 18/18 | 0.3 s |
| P3-rename-private-methods — every private method renamed (callers updated by the Roslyn renamer) | yes | 16 | 16 | 1.000 | 0 → 0 | 18/18 | 0.1 s |
| P4-rename-owner-properties — owner properties (e.g. UserId) renamed to opaque names — stresses the naming heuristics | no | 16 | 5 | 0.313 | 0 → 0 | 1/18 | 1.6 s |

## Changed units — P4-rename-owner-properties

| Unit | Before | After |
|---|---|---|
| `DELETE api/invoices/{id}` | Vulnerable | Unknown |
| `DELETE api/orders/{id}` | Vulnerable | Unknown |
| `POST api/orders/{id}/archive` | Protected | Vulnerable |
| `POST api/orders/{id}/copy-status-to/{targetid}` | Vulnerable | Unknown |
| `PUT api/invoices/{id}/amount` | Protected | Unknown |
| `PUT api/notes/{id}` | Vulnerable | Unknown |
| `PUT api/orders/{id}` | Protected | Unknown |
| `PUT api/orders/{id}/adjust-total` | Protected | Unknown |
| `PUT api/orders/{id}/status` | Vulnerable | Unknown |
| `PUT api/orders/{id}/total` | Vulnerable | Unknown |
| `PUT api/projects/{id}` | Protected | Unknown |
