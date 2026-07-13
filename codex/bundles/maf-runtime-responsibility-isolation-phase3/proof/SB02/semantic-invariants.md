# SB02 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB02-INV-01 | Runtime run entry delegates to the turn coordinator. | Source assertion transcript |
| SB02-INV-02 | Coordinator orchestration is tested without `MafAgentRuntime`. | Unit test transcript |
| SB02-INV-03 | No new `MafAgentRuntime` partial appears. | Source scan transcript |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Runtime turn request | Runtime facade | Turn coordinator | Per runtime call | Test fails if coordinator is bypassed. |
