# SB06 Proof Manifest

## Changed Files

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetWorkspaceCommandReceiptLifecycleFactExtractor.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupRuntimeExecutor.cs`
- SHA-256 `AD9496DDFB84A362E48B6AEE13BC903A607FECB1C97F74C56300719287178580` for `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptLifecycleFacts.cs`

## Behavior Moved Out Of Adapter

.NET setup execution and lifecycle receipt enrichment moved behind driver-owned runtime step and lifecycle fact extractor boundaries.

## Tests Added Or Updated

- Test name: `ProcessRuntimeArchitectureBaselineTests.WorkspaceCommandReceiptWriter_characterizes_dotnet_lifecycle_facts_in_audit_receipt`
- Test name: `DotNetSolutionSetupRuntimeExecutorTests`

## Test Transcript

- Passing transcript: `bundle://proof/SB06/transcripts/passing.txt`
- Failing-first: N/A process/non-production exemption; source assertions cover old domain leaks.

## Build Transcript

- Managed build proof: `bundle://proof/SB06/transcripts/passing.txt`

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260709182007-390484e5`
- Dependency result: `cycles: []`

## Source Assertions

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs` has no `workspace_dotnet_run` or `workspace_dotnet_stop` lifecycle enrichment.
- Production `repo://src` has no `IsDotNetRuntimeLifecycleTool`.

## Partial-Class Policy Proof

- .NET lifecycle extractor was added as a top-level module type, not as adapter partial expansion.

## Domain-Boundary Source Assertion

- .NET lifecycle facts live in `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetWorkspaceCommandReceiptLifecycleFactExtractor.cs`.

## Semantic Invariant Contract

- `bundle://proof/SB06/semantic-invariants.md`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/passing.txt`

## Risks Left Open

- Workspace command protocol still exposes `workspace_dotnet_*` tools in MAF tooling; those are protocol registrations, not process runtime domain policy.
