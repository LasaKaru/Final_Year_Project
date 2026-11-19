# Evaluation — /home/user/datasets/A-010-vuln/bitwarden-server.sln (2026-11-19)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 2 | 0 | 0 | 0 | n/a | 0.000 | 0.000 | 0.000 | n/a | n/a | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `POST billing/preview-invoice/organizations/{organizationid}/subscription/plan-change` | A-010/POST plan-change | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
| `PUT billing/preview-invoice/organizations/{organizationid}/subscription/update` | A-010/PUT update | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
