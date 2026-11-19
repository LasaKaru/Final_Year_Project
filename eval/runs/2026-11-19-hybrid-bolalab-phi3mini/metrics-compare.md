# BolaLab (development set) — symbolic vs hybrid: Phi-3-mini int4 and SmolLM2-135M int4, preliminary

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic (report) | 9 | 0 | 0 | 6 | 0 | 0 | 1.000 | 1.000 | 1.000 | 1.000 | 0.000 | 0.000 | 1.000 |
| hybrid (cpu-int4-rtn-block-32-acc-level-4, gate passed 3/4) | 9 | 1 | 0 | 5 | 0 | 0 | 0.900 | 1.000 | 1.000 | 0.947 | 0.167 | 0.100 | 1.000 |
| hybrid (smollm2-135m-instruct-int4, gate failed 2/4 → advisory) | 9 | 0 | 0 | 6 | 0 | 0 | 1.000 | 1.000 | 1.000 | 1.000 | 0.000 | 0.000 | 1.000 |
| hybrid (smollm2-135m-instruct-int4, no gate) | 9 | 1 | 0 | 5 | 0 | 0 | 0.900 | 1.000 | 1.000 | 0.947 | 0.167 | 0.100 | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 1; correctly abstained (predicted Unknown): 1; labelled units with no prediction: 0.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `PUT api/orders/{id}/status` | TC01 | Vulnerable | SYM-08 | Vulnerable | SYM-08 | ✅ |
| `PUT api/orders/{id}` | TC02 | Protected | SYM-03 | Protected | SYM-03 | ✅ |
| `DELETE api/orders/{id}` | TC03 | Vulnerable | SYM-08 | Vulnerable | SYM-08 | ✅ |
| `POST api/orders/{id}/copy-status-to/{targetid}` | TC04 | Vulnerable | SYM-08 | Vulnerable | SYM-08 | ✅ |
| `PUT api/orders/{id}/total` | TC05 | Vulnerable | SYM-08 | Vulnerable | SYM-08 | ✅ |
| `PUT api/projects/{id}` | TC06 | Protected | SYM-04 | Protected | SYM-04 | ✅ |
| `DELETE api/projects/{id}` | TC07 | Vulnerable | SYM-06 | Vulnerable | SYM-06 | ✅ |
| `PUT api/orders/{id}/status-detached` | TC08 | Vulnerable | SYM-07 | Vulnerable | SYM-07 | ✅ |
| `PUT api/documents/{id}` | TC09 | Protected | SYM-05 | Vulnerable | SYM-05 | ❌ |
| `POST api/orders/{id}/process` | TC10 | Unknown | SYM-02 | Vulnerable | SYM-02 | ❌ |
| `PUT api/notes/{id}` | TC11 | Vulnerable | SYM-08 | Vulnerable | SYM-08 | ✅ |
| `POST api/orders/{id}/archive` | TC12 | Protected | SYM-03 | Protected | SYM-03 | ✅ |
| `DELETE api/invoices/{id}` | TC13 | Vulnerable | SYM-08 | Vulnerable | SYM-08 | ✅ |
| `PUT api/invoices/{id}/amount` | TC14 | Protected | SYM-03 | Protected | SYM-03 | ✅ |
| `DELETE api/orders/{id}/hard` | TC15 | Vulnerable | SYM-07 | Vulnerable | SYM-07 | ✅ |
| `PUT api/orders/{id}/adjust-total` | TC16 | Protected | SYM-03 | Protected | SYM-03 | ✅ |
