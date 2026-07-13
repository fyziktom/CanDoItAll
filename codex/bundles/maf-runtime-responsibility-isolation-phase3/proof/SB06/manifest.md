# SB06 Proof Manifest

## Status

- Planned. Replace planned entries with captured evidence during execution.

## Required Artifacts

| Artifact | Planned path | Required before closure |
| --- | --- | --- |
| Characterization | `proof/SB06/transcripts/failing-first.txt` | Workspace plugin behavior before movement. |
| Passing tests | `proof/SB06/transcripts/passing.txt` | Tool-family and access-policy tests. |
| Host-visible smoke | `proof/SB06/transcripts/host-smoke.txt` | Command/script smoke if behavior moves. |
| Source assertions | `proof/SB06/transcripts/source-assertions.txt` | Old plugin no longer owns moved tool families. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Workspace tool invocation result | Tool family | MAF tool invocation path | Per tool call | Access denial and execution error tests. |
| Workspace access decision | Access policy service | Tool families | Per tool call/path | Protected delete and read-only mutation tests. |

## Closure Criteria

- Tool families are cohesive and tested.
- Security/access behavior is preserved.
