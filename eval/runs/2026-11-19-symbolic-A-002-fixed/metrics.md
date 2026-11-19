# Evaluation — /home/user/datasets/A-002-fixed/Oqtane.sln (2026-11-19)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 1 | 0 | 0 | 2 | 0 | 0 | 1.000 | 1.000 | 1.000 | 1.000 | 0.000 | 0.000 | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/notification/{id}` | A-002/GET | Protected |  | Protected | SYM-03 | ✅ |
| `PUT api/notification/{id}` | A-002/PUT | Vulnerable |  | Vulnerable | SYM-08,SYM-07 | ✅ |
| `DELETE api/notification/{id}` | A-002/DELETE | Protected |  | Protected | SYM-03 | ✅ |
