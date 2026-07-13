# SB03 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB03-INV-01 | Provider streaming is owned by a driver, not `MafAgentRuntime`. | Source assertion |
| SB03-INV-02 | Required finalizer repair remains bounded and governed. | Driver unit tests |
| SB03-INV-03 | Request-scoped attachments are not persisted into session state. | Session persistence negative test |
| SB03-INV-04 | Approval continuation fails predictably when no cached/rehydratable approval exists. | Approval driver negative test |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Pending approval records | Turn executor/approval driver | Approval continuation | Stored per session until resolved | Missing approval test. |
| Finalizer outcome | Finalizer repair coordinator | Runtime response | Created when governed finalizer succeeds | Missing/invalid finalizer tests. |
