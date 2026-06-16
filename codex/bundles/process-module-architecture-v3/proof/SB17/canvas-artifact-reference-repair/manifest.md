# SB17 Canvas Artifact Reference Repair

Date: 2026-06-16

## Scope

- Preserve the original desktop process editor layout by letting the Steps canvas consume the full available tab width.
- Add artifact context menu actions for cloning and highlighting artifact references on the canvas.
- Keep cloned artifact nodes as references to the same artifact key, not as duplicated artifacts or extra artifact edges.
- Exercise the dev TetrisGame project process launch path on `http://localhost:5032/`.

## Implementation

- Added `ProcessDefinitionCanvasCommandKind.CloneArtifactReference` and routed it through the typed canvas command boundary.
- Implemented artifact-reference cloning in `ProcessDefinitionCanvasEditorProjectionService` by creating a new artifact canvas node with the same `ArtifactKey`, a new `NodeKey`, `StepKey = null`, and no extra edge.
- Added local artifact-reference highlighting in `ProcessDefinitionCanvasPanel` for all visible nodes sharing the selected artifact key.
- Tightened desktop width constraints for the Steps tab, canvas stage, and shared CanvasLib workbench wrappers.
- Updated artifact node key display to prefer `ArtifactKey` so cloned references expose the shared identity consistently.

## Validation

- CodeAnalytics pre-edit snapshot: `snap-20260616123509-b505791e`.
- CodeAnalytics post-edit snapshot: `snap-20260616131702-4d969354`; no blocking errors. Remaining diagnostics are existing duplicate generated program / DI collector / large-file findings.
- Changed file hashes:
  - `changed-file-hashes.txt`
- `dotnet build src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj --no-restore`
  - `build-processes-module.txt`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "Canvas_projection_reads_steps_routes_roles_artifacts_and_toolbox|Canvas_toolbox_commands_add_elements_and_recompose|Canvas_clone_artifact_reference_adds_same_artifact_key_without_extra_edge|Canvas_rejects_stale_version_tokens" --no-restore`
  - `test-unit-canvas-artifact-reference.txt`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "Canvas_renders_shared_workbench_nodes_toolbox_selection_and_route_edges|Canvas_toolbox_action_uses_typed_canvas_command_boundary|Canvas_artifact_context_actions_clone_and_highlight_references|Canvas_recompose_uses_typed_canvas_command_boundary" --no-restore`
  - `test-components-canvas-artifact-reference.txt`
- `dotnet build CanDoItAll.slnx --no-restore`
  - `build-solution.txt`

## Browser Proof

- Desktop Steps canvas width with overlays hidden:
  - `browser/steps-canvas-desktop-no-overlays.png`
- Artifact context menu showing `Clone` and `Highlight`:
  - `browser/artifact-context-menu-clone-highlight.png`
  - `browser/contextmenu-items-after-canvas-dispatch.json`
- Clone action proof:
  - `browser/clone-action-result.json`
- Highlight action proof:
  - `browser/artifact-highlight-result.png`
  - `browser/highlight-action-result.json`

## TetrisGame Process Launch

- Project found in dev DB:
  - `TetrisGame`
  - Project id: `3324868f-66e2-478a-bb8f-14f32a5db1e9`
- Project structure captured:
  - `api/tetris-structure.json`
- Attempted launch:
  - `POST /api/project-structure/projects/3324868f-66e2-478a-bb8f-14f32a5db1e9/nodes/custom:cfd406780f034384a70ea6b87507422a/process/start`
  - Body: `{"runHrMatch":true,"execute":true,"includeLaunchPlan":true,"requestedBy":"codex-process-ui-repair"}`
- Result:
  - HTTP `410 Gone`
  - Error code: `ProcessModuleRewriteInProgress`
  - Response proof: `api/tetris-process-start-410-full.json`
- Source blocker:
  - `src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessNodeService.cs`
  - The service currently throws a `ProjectStructureAgentException` with status `410` instead of calling a rebuilt process launch/application layer.
- Architecture phase blocker:
  - The current bundle README says SB01-SB19 are complete and SB20 is next.
  - Launch planning, runtime execution views, project-scoped process integration, and final E2E regression are owned by later SB21-SB28 packages.
  - `ProcessWorkspaceShellProjectionService` currently sets `CanLaunchRuns = false` and reports that projection store/runtime integration is pending.

The TetrisGame process did not run internal agents because the current branch has no active project-structure-to-process launch implementation. This is a real runtime gap outside the canvas UI repair.
