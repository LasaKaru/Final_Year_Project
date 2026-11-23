# Evaluation — /home/user/datasets/A-005-vuln/Kavita.sln (2026-11-23)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 5 | 0 | 0 | 0 | 0 | 0 | 1.000 | 1.000 | 1.000 | 1.000 | n/a | 0.000 | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `DELETE api/filter` | A-005/DELETE filter | Vulnerable |  | Vulnerable | SYM-08 | ✅ |
| `POST api/collection/update-series` | A-005/POST update-series | Vulnerable |  | Vulnerable | SYM-08 | ✅ |
| `POST api/stream/update-dashboard-stream` | A-005/POST update-dashboard-stream | Vulnerable |  | Vulnerable | SYM-08 | ✅ |
| `POST api/stream/update-sidenav-stream` | A-005/POST update-sidenav-stream | Vulnerable |  | Vulnerable | SYM-08 | ✅ |
| `POST api/stream/bulk-sidenav-stream-visibility` | A-005/POST bulk-sidenav-stream-visibility | Vulnerable |  | Vulnerable | SYM-08 | ✅ |
