# SB01 Proof Manifest

## Status

Completed.

## Owned Requirements And Raw Notes

- Requirements: RQ01, RQ02
- Raw notes: N001, N003, N007
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` defines `ProcessStepOperation`, `ProcessStepTargetScope`, and `ProcessStepOperationContract`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` emits `agentProcessStepAllowedOperations`, `agentProcessStepTargetScope`, and `agentProcessStepAllowsProductMutation`.
- `repo://src/CanDoItAll.Modules.Processes/Definitions/ProcessDefinitionLinter.cs` keeps generic artifact creation from being treated as product mutation without a target mutation signal.
- Transcript: `bundle://proof/SB01/transcripts/source-assertions.txt`

## Production Behavior Artifact Matrix

| Artifact/signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessStepOperationContract` metadata | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs` | `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` and `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs` | Dispatch metadata is created before each process-step AgentFramework invocation | `bundle://proof/SB01/transcripts/failing-first.txt` covers generic business artifact creation without product mutation |

## Failing-First Or Red-Team Proof

Transcript: `bundle://proof/SB01/transcripts/failing-first.txt`

## Passing Proof

Transcript: `bundle://proof/SB01/transcripts/passing.txt`

## Anti-Stub Audit

Transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

Transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

Representative changed-file SHA-256: `355a8ae050d3bec7961e9f138afde1266083d8bfa0984b385a8923e645b18800`

## Validation

Completed through focused integration tests, unit tests, full build, SQLite audit, and completed bundle validator.

## Blockers

None.
