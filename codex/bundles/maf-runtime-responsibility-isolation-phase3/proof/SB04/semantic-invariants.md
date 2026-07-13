# SB04 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB04-INV-01 | Normal runtime build is owned by a build coordinator. | Unit/source proof |
| SB04-INV-02 | Handoff build is isolated and validates metadata. | Handoff tests |
| SB04-INV-03 | Script policy inspection is directly testable with fake file/path dependencies. | Unit tests |
| SB04-INV-04 | Factory does not remain the hidden god object. | Source assertion and CodeAnalytics comparison |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Runtime build result | Build coordinator | Turn execution | Per run | Missing field/provider model tests. |
