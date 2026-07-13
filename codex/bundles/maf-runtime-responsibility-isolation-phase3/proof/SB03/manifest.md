# SB03 Proof Manifest

## Status

- Planned. Replace planned entries with captured evidence during execution.

## Required Artifacts

| Artifact | Planned path | Required before closure |
| --- | --- | --- |
| Characterization | `proof/SB03/transcripts/failing-first.txt` | Current streaming/finalizer/session/approval behavior. |
| Passing tests | `proof/SB03/transcripts/passing.txt` | Direct driver tests and focused integration smoke. |
| Source assertions | `proof/SB03/transcripts/source-assertions.txt` | Moved behavior no longer lives in runtime. |
| Anti-stub audit | `proof/SB03/transcripts/anti-stub.txt` | Production path uses drivers. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Runtime response assembly | Turn executor | Turn coordinator/runtime facade | Per provider run | Empty provider completion and approval tests. |
| Serialized session state | Session persistence driver | Chat/session persistence caller | Per run when persistable | Timeout/scrub negative tests. |
| Pending approval records | Approval driver | Approval continuation path | Created when tools request approval | Missing cache/rehydration negative tests. |

## Closure Criteria

- Streaming, finalizer, session, and approval drivers are directly tested.
- Runtime no longer owns moved methods.
