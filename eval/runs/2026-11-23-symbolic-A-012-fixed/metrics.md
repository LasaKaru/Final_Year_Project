# Evaluation — /home/user/datasets/A-012-fixed/bitwarden-server.sln (2026-11-23)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 0 | 0 | 0 | 2 | n/a | n/a | n/a | n/a | n/a | n/a | 0.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `POST providers/{providerid}/clients/existing` | A-012/POST existing | Protected |  | Unknown | SYM-02 | ❌ |
| `POST providers/{providerid}/organizations/add` | A-012/POST add | Protected |  | Unknown | SYM-02 | ❌ |
