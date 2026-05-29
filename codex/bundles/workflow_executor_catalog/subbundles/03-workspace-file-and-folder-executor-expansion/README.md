# 03-workspace-file-and-folder-executor-expansion

## Status

- Status: `Completed`

## Closure Notes

- Expanded workspace file operations for exists, tree, directory creation, delete, copy, move, hash, zip, and unzip.
- Kept all operations behind workspace path normalization, explicit recursive delete semantics, dry-run coverage, and bounded enumeration.
- Updated executor settings/result models and descriptor metadata for deterministic UI/schema behavior.
- Proof manifest: `bundle://proof/SB03/manifest.md`
- Semantic invariants: `bundle://proof/SB03/semantic-invariants.md`

## Objective

Make local workspace file and folder workflows practical while preserving sandbox boundaries.

## Covered Inputs

- RN02: Expand obvious workflow executors.
- RN03: Verify and complete local folder/file workflow support.
- R3: Add common workspace file/folder operations.
- R4: Support folder scenarios with safe include/exclude and truncation behavior.

## Prerequisites

- SB01 closure gate passed.
- SB02 closure gate passed for any operation that emits content artifacts.
- Existing workspace path policy and file service behavior are reviewed.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/WorkspaceFileWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/SourceIngestionWorkflowExecutor.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/BuiltInWorkflowExecutorDescriptors.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileService.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workspace/Paths/WorkspacePathPolicy.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ManagedFilesStorageIntegrationTests.cs`

## Scope

- Extend `storage.file` unless source audit proves a split executor is cleaner.
- Add operations for exists, tree, create/ensure directory, delete, copy, move/rename, hash, zip, and unzip where safe.
- Add settings for source, destination, recursive behavior, dry run, include/exclude globs, max files, max bytes, and overwrite behavior.
- Keep default behavior workspace-scoped only.
- Add normalized file/folder result shapes with metadata.

## Dependency Impact

- SB04/SB05/SB07/SB09/SB10 depend on practical file/folder operations for data shaping, reports, downloads, templates, and final scenario proof.
- Weak path safety here invalidates all later local workflow proof.

## Validation Depth

- Unit tests against sandbox workspace for every new operation.
- Negative tests for path traversal, absolute path escape, unsafe recursive deletion, overwrite behavior, and max-file/max-byte limits.
- Critical semantic proof with downstream smoke because templates and scenario harness depend on this surface.

## Implementation Steps

1. Audit current `storage.file` settings and result schemas.
2. Choose extend-versus-split based on existing descriptor and UI assumptions.
3. Implement safe operations through existing workspace services and path policy.
4. Add include/exclude glob handling and bounded tree enumeration.
5. Add tests for success, dry-run deletion, recursive confirmation, traversal, absolute paths, and binary/file-reference boundaries.
6. Update descriptors and schema metadata.

## Do Not Do

- Do not allow arbitrary host absolute paths by default.
- Do not delete directories recursively without explicit recursive confirmation and dry-run coverage.
- Do not implement command execution in this phase.
- Do not bypass existing workspace path normalization.

## Acceptance Checklist

- Users can list, read, write, create, copy, move, rename, hash, zip, unzip, and delete workspace files/folders safely.
- Deletion supports dry run and requires explicit recursive confirmation for directories.
- Tree and glob operations are bounded and return normalized metadata.
- Path traversal and absolute path escape are tested.
- Result schemas are deterministic and documented in descriptors.

## Proof Required

- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB03/semantic-invariants.md`
- Failing-first or explicit gap transcript for missing folder operation.
- Passing targeted executor transcript covering success and negative safety cases.
- Changed-file hashes, source assertions, anti-stub audit, and one dependent-flow smoke for SB09/SB10 readiness.

## Browser Validation Logging

- N/A unless the workflow authoring UI changes in this phase; if it does, add route, viewport, actions, screenshots, and result to `bundle://reviews/01-execution-report.md`.

## Progression Gate

- Continue to SB04 only after workspace file/folder operations are safe, bounded, and tested against both positive and escape/deletion-negative scenarios.

## Suggested Agent Prompt

Use SB03 to close practical workspace file/folder gaps through existing path-policy services. Keep the change scoped, make destructive operations explicit, and prove both useful workflows and safety failures.
