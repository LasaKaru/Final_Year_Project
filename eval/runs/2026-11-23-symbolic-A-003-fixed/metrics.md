# Evaluation — /home/user/datasets/A-003-fixed/Oqtane.sln (2026-11-23)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 0 | 1 | 0 | 0 | n/a | n/a | n/a | n/a | 0.000 | n/a | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/user/{id}` | A-003/GET | Protected |  | OutOfScope | SYM-01 | ❌ |
