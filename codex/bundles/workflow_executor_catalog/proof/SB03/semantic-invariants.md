# SB03 Semantic Invariants

## Invariant SB03-WORKSPACE-SCOPE

- Invariant ID: `SB03-WORKSPACE-SCOPE`
- Source raw note: RN03 and R3 require practical local file/folder operations without host filesystem escape.
- Expected behavior: all file operations resolve through workspace path policy and destructive operations require explicit settings, with dry-run coverage.
- Disallowed shallow implementation: adding executor operation names while bypassing existing workspace services or allowing unbounded recursive/delete behavior.
- Failing-first test: N/A - process/non-production exemption because the missing operations were audited from source and then covered with positive/negative executor tests.
- Passing test: `WorkspaceFileExecutorSupportsDirectoryHashZipAndDryRunDelete` in `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileContracts.cs`; `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileService.cs`; `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/WorkspaceFileWorkflowExecutor.cs`; `repo://src/CanDoItAll.AgentFramework.Models/Workspace/WorkspaceFileToolModels.cs`.
- Production assertions: `bundle://proof/SB10/transcripts/source-assertions-workspace-file-ops.txt`.
- Red-team negative case: dry-run delete and workspace-scoped service tests reject unsafe deletion/escape behavior in `WorkspaceFileExecutorSupportsDirectoryHashZipAndDryRunDelete`.
- Downstream dependency check: SB09 templates and SB10 scenario harness use the expanded workspace executor surface.
