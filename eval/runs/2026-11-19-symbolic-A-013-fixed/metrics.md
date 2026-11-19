# Evaluation — /home/user/datasets/A-013-fixed/Kavita.sln (2026-11-19)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 0 | 7 | 0 | 0 | n/a | n/a | n/a | n/a | 0.000 | n/a | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/download/volume-size` | A-013/GET volume-size | Protected |  | OutOfScope | SYM-01 | ❌ |
| `GET api/download/chapter-size` | A-013/GET chapter-size | Protected |  | OutOfScope | SYM-01 | ❌ |
| `GET api/download/series-size` | A-013/GET series-size | Protected |  | OutOfScope | SYM-01 | ❌ |
| `GET api/download/volume` | A-013/GET volume | Protected |  | Protected | SYM-05 | ✅ |
| `GET api/download/chapter` | A-013/GET chapter | Protected |  | Protected | SYM-05 | ✅ |
| `GET api/download/series` | A-013/GET series | Protected |  | Protected | SYM-05 | ✅ |
| `GET api/chapter` | A-013/GET chapter | Protected |  | OutOfScope | SYM-01 | ❌ |
