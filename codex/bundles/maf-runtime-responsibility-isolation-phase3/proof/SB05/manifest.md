# SB05 Proof Manifest

## Status

- Planned. Replace planned entries with captured evidence during execution.

## Required Artifacts

| Artifact | Planned path | Required before closure |
| --- | --- | --- |
| Characterization | `proof/SB05/transcripts/failing-first.txt` | Access/descriptor/attachment behavior before movement. |
| Passing tests | `proof/SB05/transcripts/passing.txt` | Access planner, descriptor catalog, orchestrator tests. |
| Source assertions | `proof/SB05/transcripts/source-assertions.txt` | No final composer partial and old ownership removed. |
| Extension seam | `proof/SB05/transcripts/extension-seam.txt` | Fake capability contributor registration without old edits. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Capability access plan | Access planner | Attachment orchestrator | Per runtime build | Denied workspace/process-intent tests. |
| Capability descriptors | Descriptor catalog | UI/runtime diagnostics and access plan | Per capability catalog snapshot | Unknown/duplicate descriptor tests. |

## Closure Criteria

- `RuntimeCapabilityComposer` is not a final partial boundary.
- Capability extension seam is proven.
