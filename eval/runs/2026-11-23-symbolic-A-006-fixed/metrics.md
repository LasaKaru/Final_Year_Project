# Evaluation — /home/user/datasets/A-006-fixed/Kavita.sln (2026-11-23)

| Arm | TP | FP | FN | TN | U(vuln) | U(prot) | Precision | Recall | Op. recall | F1 | FPR | FDR | Coverage |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| symbolic | 0 | 0 | 0 | 3 | 0 | 0 | n/a | n/a | n/a | n/a | 0.000 | n/a | 1.000 |

Units labelled Unknown/OutOfScope (excluded from the matrix): 0; correctly abstained (predicted Unknown): 0; labelled units with no prediction: 1.

| Unit | TC | Label | Expected rule | Predicted | Rules | Match |
|---|---|---|---|---|---|---|
| `GET api/readinglist/items` | A-006/GET items | Protected |  | OutOfScope | SYM-01 | ❌ |
| `POST api/download/bookmarks` | A-006/POST bookmarks | Protected |  | — |  | ❌ |
| `GET api/annotation/{annotationid}` | A-006/GET {annotationId} | Protected |  | OutOfScope | SYM-01 | ❌ |
