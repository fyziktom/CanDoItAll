# SB02 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs
- repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs
- repo://src/CanDoItAll.AgentFramework.Core/Workspace/Audit/WorkspaceExecutionAuditContext.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs
- repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB02 runtime governance artifact | repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs | repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs and bundle://proof/SB02/transcripts/passing.txt | Verified by bundle://proof/SB02/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB02/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB02/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB02/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB02/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_SB02_INV_001_denies_validation_without_run_validation_operation`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_SB02_INV_002_allows_validation_when_run_validation_operation_is_allowed`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_SB02_INV_003_denies_runtime_launch_without_launch_runtime_operation`
- Test name: `CanDoItAll.Tests.Unit.AgentToolInvocationPolicyTests.EvaluateAsync_SB02_INV_004_allows_artifact_only_write_under_current_run_artifacts`

## Anti-Stub Audit

- bundle://proof/SB02/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB02/transcripts/changed-file-hashes.txt
- `2ab3f2d6e98713abfc4968c15b5633839d5f49fe4788731295476f78a0218f93`  `repo://src/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs`
- `eee2ae3e819c25763041a59a0df4a73acfd43850ade942cc004c893a6a1841e2`  `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
- `834d86d0959e3672f7b21ebed2b33f3bdb0a99f3b233d3dab3821eada9651f43`  `repo://tests/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs`

## Validation

- Focused proof commands passed for SB02; see bundle://proof/SB02/transcripts/passing.txt.
- Source assertions passed for SB02; see bundle://proof/SB02/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB02/transcripts/anti-stub-audit.txt.

## Blockers

None.
