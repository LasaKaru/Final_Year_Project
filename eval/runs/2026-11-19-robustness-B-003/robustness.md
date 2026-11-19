# Robustness — /home/user/datasets/B-003-v10.3.2/Oqtane.slnx

Units = endpoints with at least one traced path; a unit is unchanged when its worst symbolic verdict is the same.
Semantics-preserving perturbations should give stability 1.000; P4 measures dependence on owner-name heuristics.

| Perturbation | Preserves semantics | Units | Unchanged | Stability | Compile errors before → after | Identical slices (line numbers ignored) | Time |
|---|---|---|---|---|---|---|---|
| P1-rename-locals — every local variable renamed to an opaque name (v1, v2, …) | yes | 138 | 138 | 1.000 | 47 → 47 | 15/182 | 10.5 s |
| P2-misleading-comments — a comment claiming the ownership was verified before every statement | yes | 138 | 138 | 1.000 | 47 → 47 | 182/182 | 4.2 s |
| P3-rename-private-methods — every private method renamed (callers updated by the Roslyn renamer) | yes | 138 | 138 | 1.000 | 47 → 47 | 114/182 | 10.8 s |
| P4-rename-owner-properties — owner properties (e.g. UserId) renamed to opaque names — stresses the naming heuristics | no | 138 | 123 | 0.891 | 47 → 47 | 23/181 | 7.0 s |

## Changed units — P4-rename-owner-properties

| Unit | Before | After |
|---|---|---|
| `DELETE api/file/{id}` | Vulnerable | Unknown |
| `DELETE api/folder/{id}` | Vulnerable | Unknown |
| `DELETE api/htmltext/{id}/{moduleid}` | Vulnerable | Unknown |
| `DELETE api/module/{id}` | Vulnerable | Unknown |
| `DELETE api/notification/{id}` | Protected | Unknown |
| `DELETE api/page/{id}` | Vulnerable | Unknown |
| `DELETE api/pagemodule/{id}` | Vulnerable | Unknown |
| `DELETE api/profile/{id}` | Vulnerable | Unknown |
| `DELETE api/role/{id}` | Vulnerable | Unknown |
| `DELETE api/user/{id}` | Vulnerable | Unknown |
| `GET api/module/{id}` | Vulnerable | Unknown |
| `POST api/page` | Vulnerable | Unknown |
| `POST api/page/{frompageid}/{topageid}/{usepagepermissions}` | Vulnerable | Unknown |
| `POST api/page/{id}` | Vulnerable | Unknown |
| `PUT api/notification/{id}` | Protected | Vulnerable |
