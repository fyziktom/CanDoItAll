# Run Folder Artifact Projection

## Status

- `Completed`

## Objective

- Project process run nodes should expose useful run workspace folder nodes instead of one node per artifact file directory.

## Success Criteria

- `artifacts/.../process-runs/{runId}/file.md` collapses to `artifacts/.../process-runs/{runId}`.
- `output/.../process-runs/{runId}/TetrisGame/Components/App.razor` collapses to `output/.../process-runs/{runId}/TetrisGame`.
- Date-based receipt paths without the current run id do not create run folder nodes.
- Existing ability to open a projected managed folder remains.

## Covered Inputs

- R3 Run Folder Projection.
- Raw note 3.

## Prerequisites

- Prepared bundle validator passes.
- SB01 complete or unchanged enough that run path assumptions still hold.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAssemblyService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Deliverables

- Run-aware managed folder path collapse.
- Integration test covering artifact root, generated product root, and ignored date receipt folder.

## Dependency Impact

- Project structure graph readability and file explorer opening depend on this grouping.
- Weak proof would keep the user's process node cluttered and make run artifact folders hard to find.

## Validation Depth

- Project-structure integration regression.

## Implementation Steps

1. Update the existing process output folder projection test to include multiple files under nested product subfolders and date receipt folders.
2. Change folder resolution to use run id path segments.
3. Confirm projected node count and storage references.
4. Run targeted workbench integration tests.

## Scope Exceptions

- Does not delete existing user-authored artifact nodes already created by previous process runs.

## Do Not Do

- Do not remove process artifact records.
- Do not hide run-level artifact or generated product folders.

## Acceptance Checklist

- The fixture projects exactly the expected run folder nodes.
- No node is projected for `artifacts/.../process-runs/20260528/...` when the run id is absent.
- Storage reference still points at a managed workspace folder.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`

## Browser Validation Logging

- N/A for layout. Optional project structure browser smoke may verify folder count for the live Tetris run after restart.

## Progression Gate

- Passed. Workbench integration test proves run-root folder projection and no per-artifact subfolder nodes for the representative path set.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
