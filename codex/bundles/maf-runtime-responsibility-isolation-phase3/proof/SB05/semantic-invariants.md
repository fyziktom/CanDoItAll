# SB05 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB05-INV-01 | `RuntimeCapabilityComposer` is not a partial-class final boundary. | Source assertion |
| SB05-INV-02 | Access planning is directly testable. | Access planner tests |
| SB05-INV-03 | Descriptor mapping is directly testable. | Descriptor catalog tests |
| SB05-INV-04 | New capability contribution does not edit old monoliths. | Extension seam test |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Capability access plan | Access planner | Capability attachment | Per runtime build | Denial policy tests. |
