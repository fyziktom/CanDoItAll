# SB06 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB06-INV-01 | Workspace mutation tools honor access profiles. | Policy tests |
| SB06-INV-02 | Protected directory recursive delete remains denied. | Negative test |
| SB06-INV-03 | Command/script tools remain host-visible and bounded. | Host smoke transcript |
| SB06-INV-04 | Adding a tool family avoids old plugin edits. | Extension seam test |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Workspace access decision | Policy service | Tool set | Per invocation | Denial tests. |
