# Evaluation — /home/user/datasets/A-001-vuln/Oqtane.slnx (2026-11-19)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 1 | 0 | 0 | 0 | 0 | 0 | 1.000 | 1.000 | 1.000 | 1.000 | n/a | 0.000 | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `PUT api/notification/{id}` | A-001/PUT | Vulnerable |  | Vulnerable | SYM-07,SYM-08 | ✅ |
