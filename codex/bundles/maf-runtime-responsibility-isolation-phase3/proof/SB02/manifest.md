# SB02 Proof Manifest

## Status

- Planned. Replace planned entries with captured evidence during execution.

## Required Artifacts

| Artifact | Planned path | Required before closure |
| --- | --- | --- |
| Failing-first/characterization | `proof/SB02/transcripts/failing-first.txt` | Existing runtime entry behavior covered before movement. |
| Passing tests | `proof/SB02/transcripts/passing.txt` | Direct coordinator tests and focused MAF tests. |
| Source assertions | `proof/SB02/transcripts/source-assertions.txt` | Runtime delegates and no new partials. |
| Anti-stub audit | `proof/SB02/transcripts/anti-stub.txt` | Coordinator is production-wired and not test-only. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Turn request/result records | `MafRuntimeTurnCoordinator` | `MafAgentRuntime`, executor | Created per run | Coordinator negative tests. |

## Closure Criteria

- `MafAgentRuntime` facade delegation is proven.
- Direct coordinator tests do not construct `MafAgentRuntime`.
