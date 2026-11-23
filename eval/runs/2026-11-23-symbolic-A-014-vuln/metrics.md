# Evaluation — /home/user/datasets/A-014-vuln/IoTSharp.slnx (2026-11-23)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 1 | 0 | 0 | 0 | 2 | 0 | 1.000 | 1.000 | 0.333 | 1.000 | n/a | 0.000 | 0.333 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/customers/{id}` | A-014/GET {id} | Vulnerable |  | Unknown | SYM-09 | ❌ |
| `POST api/customers/tenant/{tenantid}/all` | A-014/POST all | Vulnerable |  | Unknown | SYM-09 | ❌ |
| `GET api/devices/{deviceid}/attributelatest` | A-014/GET attributelatest | Vulnerable |  | Vulnerable | SYM-08 | ✅ |
