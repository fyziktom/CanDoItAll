# SB04 Proof Manifest

## Status

Completed.

## Owned Requirements And Raw Notes

- Requirements: RQ06, RQ07
- Raw notes: N002, N004, N007
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` maps workflow artifacts to process artifact expectations and writes provenance.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` prevents stale subprocess projection by requiring the current subprocess run id.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` validates workflow and subprocess producers through process-owned completion finalization.
- Transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Workflow/subprocess artifact projection lineage | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Workflow-backed and subprocess-parent roles enter the same process finalizer | `bundle://proof/SB04/transcripts/failing-first.txt` covers wrong producer/run lineage rejection |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB04/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB04/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB04/transcripts/changed-file-hashes.txt`

Representative changed-file SHA-256: `ef9be116e135251dff776f9f7ee338a114c5fd0b3f8c9fcb34f53ade04a296d3`

## Validation

Completed through focused integration tests and build validation.

## Blockers

None.
