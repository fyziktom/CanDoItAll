# SB05 Proof Manifest

- Subbundle: `SB05`
- Status: `Completed`
- Owned requirements: R2, R6
- Raw notes: RN02
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`

## Changed Source

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MarkdownRenderWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`

## Command Transcripts

- Source assertions: `bundle://proof/SB10/transcripts/source-assertions-executor-implementations.txt`
- Unit proof: `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`
- Scenario proof: `bundle://proof/SB10/transcripts/dotnet-test-scenario-harness-workflow-executor-catalog.txt`
- Changed-file hashes: `bundle://proof/SB10/transcripts/changed-file-hashes.txt`

## Production Behavior Artifact Matrix

| Artifact/state | Producer | Reader | Proof |
| --- | --- | --- | --- |
| Markdown output file artifact | `MarkdownRenderWorkflowExecutor`; `MafInProcessWorkflowExecutionBackend` | Workspace file service and workflow run artifact metadata | `MarkdownRenderExecutorRendersTablesAndWritesOutputFile`; `MafBackendRecordsConfiguredFileArtifactsForCompletedFileWrites` |

## Closure Result

`markdown.render` produces deterministic markdown and can persist report output to workspace files while the runtime records file artifacts.
