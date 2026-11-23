# Evaluation — /home/user/datasets/A-013-vuln/Kavita.sln (2026-11-23)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 7 | 0 | 0 | 0 | 0 | 0 | 1.000 | 1.000 | 1.000 | 1.000 | n/a | 0.000 | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/download/volume-size` | A-013/GET volume-size | Vulnerable |  | Vulnerable | SYM-11 | ✅ |
| `GET api/download/chapter-size` | A-013/GET chapter-size | Vulnerable |  | Vulnerable | SYM-11 | ✅ |
| `GET api/download/series-size` | A-013/GET series-size | Vulnerable |  | Vulnerable | SYM-11 | ✅ |
| `GET api/download/volume` | A-013/GET volume | Vulnerable |  | Vulnerable | SYM-11 | ✅ |
| `GET api/download/chapter` | A-013/GET chapter | Vulnerable |  | Vulnerable | SYM-11 | ✅ |
| `GET api/download/series` | A-013/GET series | Vulnerable |  | Vulnerable | SYM-11 | ✅ |
| `GET api/chapter` | A-013/GET chapter | Vulnerable |  | Vulnerable | SYM-11 | ✅ |
