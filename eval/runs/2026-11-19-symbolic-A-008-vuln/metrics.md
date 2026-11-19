# Evaluation — /home/user/datasets/A-008-vuln/bitwarden-server.slnx (2026-11-19)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 0 | 0 | 1 | 0 | n/a | n/a | 0.000 | n/a | n/a | n/a | 0.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `POST ciphers/create` | A-008/POST create | Vulnerable |  | Unknown | SYM-02 | ❌ |
