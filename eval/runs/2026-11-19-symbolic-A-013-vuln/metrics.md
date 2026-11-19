# Evaluation — /home/user/datasets/A-013-vuln/Kavita.sln (2026-11-19)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 3 | 0 | 4 | 0 | n/a | 0.000 | 0.000 | 0.000 | n/a | n/a | 0.429 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/download/volume-size` | A-013/GET volume-size | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
| `GET api/download/chapter-size` | A-013/GET chapter-size | Vulnerable |  | Unknown | SYM-09 | ❌ |
| `GET api/download/series-size` | A-013/GET series-size | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
| `GET api/download/volume` | A-013/GET volume | Vulnerable |  | Unknown | SYM-09 | ❌ |
| `GET api/download/chapter` | A-013/GET chapter | Vulnerable |  | Unknown | SYM-09 | ❌ |
| `GET api/download/series` | A-013/GET series | Vulnerable |  | Unknown | SYM-09 | ❌ |
| `GET api/chapter` | A-013/GET chapter | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
