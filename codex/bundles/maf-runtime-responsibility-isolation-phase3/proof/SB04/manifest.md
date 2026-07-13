# SB04 Proof Manifest

## Status

- Planned. Replace planned entries with captured evidence during execution.

## Required Artifacts

| Artifact | Planned path | Required before closure |
| --- | --- | --- |
| Characterization | `proof/SB04/transcripts/failing-first.txt` | Runtime build/handoff/finalizer/script-policy behavior. |
| Passing tests | `proof/SB04/transcripts/passing.txt` | Build coordinator, handoff builder, instrumentor, script policy tests. |
| Source assertions | `proof/SB04/transcripts/source-assertions.txt` | Old factory no longer owns moved internals. |
| Handoff smoke | `proof/SB04/transcripts/handoff-smoke.txt` | Handoff integration slice. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Runtime build result | Build coordinator | Turn coordinator/executor | Created per run | Missing model/invalid handoff tests. |
| Tool ownership metadata | Instrumentor | Tool trace recorder/runtime response | Created during runtime build | Duplicate/unknown tool negative tests. |

## Closure Criteria

- Factory responsibilities are split and tested.
- `IServiceProvider` is not a broad service locator in construction behavior.
