# Evaluation — /home/user/datasets/A-014-vuln/IoTSharp.slnx (2026-11-23)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 3 | 0 | 0 | 0 | n/a | 0.000 | 0.000 | 0.000 | n/a | n/a | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/customers/{id}` | A-014/GET {id} | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
| `POST api/customers/tenant/{tenantid}/all` | A-014/POST all | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
| `GET api/devices/{deviceid}/attributelatest` | A-014/GET attributelatest | Vulnerable |  | OutOfScope | SYM-01 | ❌ |
