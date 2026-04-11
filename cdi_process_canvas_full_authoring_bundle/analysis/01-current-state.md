# Current State

## Live Bundle State

- The prior branch-focused initiative bundle exists at `C:\repositories\CanDoItAll\cdi_process_branching_canvas_bundle`.
- This new bundle exists at `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle`.
- The new bundle was scaffolded with the `initiative` profile and now replaces placeholder structure with execution-ready analysis and subbundle sequencing.

## Process Canvas Node Inventory Today

- Definition canvas node kinds in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
  - `process-step`
  - `process-branch-router`
  - `process-role`
- Runtime canvas node kinds in the same file
  - `process-run-step`
  - `process-run-branch-router`

## Current Advanced-Port Coverage

- `process-branch-router`
  - Already projects explicit `InputPorts` and `OutputPorts`.
  - Definition inputs include `From step` and `Decision maker`.
  - Definition outputs include one route per configured branch outcome plus `Default` and `Error` where appropriate.
- `process-role`
  - Currently has only one named output port for decision authority.
  - No participant-role outputs for `Responsible`, `Reviewer`, `Approver`, or `Backup`.
- `process-step`
  - Still behaves as a legacy anchor-based node in practice.
  - Does not expose named participant, artifact, or structural ports in the current projection.
- `process-run-step`
  - Does not expose the richer multi-port semantics needed for parity with a generalized definition canvas.
- `process-run-branch-router`
  - Exposes only the branch-router subset, not the generalized role and step semantics.

## Current Canvas Authoring Behavior

- Definition-canvas authoring in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs` currently understands only three connection families:
  - direct step dependency
  - routed branch-outcome dependency
  - role decision-authority assignment to a branch router
- Relevant methods:
  - `CreateDefinitionCanvasConnection`
  - `TryAssignDecisionAuthorityConnection`
  - `TryCreateRoutedStepDependencyConnection`
  - `TryCreateDirectStepDependencyConnection`
  - `DeleteDefinitionCanvasLink`
  - `HandleCanvasNodesMovedAsync`

## Canonical Process Model Truth

- Step kinds in `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
  - `Start`
  - `Work`
  - `Decision`
  - `Approval`
  - `Review`
  - `Delivery`
  - `End`
- Role responsibility kinds in the same file
  - `Responsible`
  - `Reviewer`
  - `Approver`
  - `Backup`
- Artifact kinds in the same file
  - `Brief`
  - `Evidence`
  - `Decision`
  - `Deliverable`
  - `Transcript`
  - `Checklist`
  - `Prompt`
  - `Dataset`
  - `Other`

## Canonical Relationship Support Today

- Already canonical and persistable
  - Step-to-step dependencies through `ProcessStepDependencyDefinition`
  - Outcome-qualified routed dependencies through `ProcessStepDependencyDefinition` plus branch outcome identifiers
  - Step role participation through `ProcessStepRoleAssignmentRequirement`
  - Single decision-maker role assignment on a step through `DecisionRoleRequirementId`
  - Step and role positions through `CanvasX` and `CanvasY`
  - Branch-router derived-node position through `BranchCanvasX` and `BranchCanvasY`
- Not currently canonical as an explicit graph relationship
  - Artifact-consumption links from one step's artifact output to another step's artifact input
  - Rich per-port cardinality rules encoded as typed process-canvas semantics instead of local UI heuristics

## Current Form-Only Editing Surfaces

- Step editor form already exposes:
  - branch outcomes
  - dependencies
  - role assignments
  - artifact expectations
  - decision maker
- Relevant files
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepRoleAssignmentEditor.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessArtifactExpectationEditor.razor`
  - `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessRoleEditorForm.razor`

## Current Scenario Inventory That Can Prove The Work

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.Scenarios.cs` already contains realistic software-development scenarios including:
  - software delivery
  - branching code review
  - hotfix rollout
  - onboarding
  - incident response
- These scenarios already contain the role assignments, dependencies, and artifact expectations needed to validate generalized canvas authoring.

## Current Gap Summary

- The canvas can currently author only a narrow subset of the process graph.
- Role participation is canonical but not first-class on the canvas.
- Structural step semantics are canonical but still rendered through generic node anchors instead of typed ports.
- Artifact expectations exist, but downstream artifact-consumption relationships are not canonical yet.
- Runtime projection does not yet mirror the richer graph semantics needed if the definition canvas becomes the primary editor.
