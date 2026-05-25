# SB03 Proof Manifest

## Status

Completed.

## Owned Requirements And Raw Notes

- Requirements: RQ05
- Raw notes: N002, N006, N007
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Models.cs` adds `ArtifactProjectionLineage`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` prefixes manager recovery artifacts with recovery and recovered-for execution run identifiers.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` validates manager recovery artifacts with explicit recovery lineage.
- Transcript: `bundle://proof/SB03/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `manager-recovery-artifact` lineage key | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` | Manager recovery dispatch projects recovered artifacts through the same finalizer path | `bundle://proof/SB03/transcripts/failing-first.txt` covers wrong execution-run rejection and recovery-specific source assertions |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB03/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB03/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB03/transcripts/changed-file-hashes.txt`

Representative changed-file SHA-256: `f476c311e48d191361e0c54f847374bd4f4b6d724f352ed3993a3664584ab2ac`

## Validation

Completed through focused integration tests, source assertions, and build validation.

## Blockers

None.
