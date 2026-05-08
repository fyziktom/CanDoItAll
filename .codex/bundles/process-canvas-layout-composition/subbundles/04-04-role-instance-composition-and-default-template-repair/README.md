# 04 Role Instance Composition And Default Template Repair

## Status

- `Completed`

## Objective

- Replace distant single-role canvas hubs with multiple visual role nodes that still resolve to the same role contract, and repair saved default process template coordinates so default canvases start clearer.

## Covered Inputs

- `N008`
- `N009`
- `N010`
- `REQ-007`
- `REQ-008`

## Prerequisites

- `01-layout-analysis-and-contract` completed.
- `02-definition-recomposition-tuning` completed.
- `03-validation-and-browser-proof` completed with residual role/edge crossing risk recorded.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasBranching.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasSurfaceFactory.Links.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasSurfaceFactory.Ports.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\ProcessCanvasRecompositionService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\WebGl\ProcessWebGlLayoutEngine.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas\WebGl\ProcessWebGlSceneAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\ai-assisted-change-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\architecture-decision-governance\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\branching-code-review\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\business-plan-development\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\customer-onboarding\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-development-slice\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-feature-function-implementation\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\dotnet-solution-setup\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\hotfix-rollout\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\incident-response\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\oss-intake-supply-chain-governance\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\release-readiness-and-deployment\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasRecompositionServiceTests.cs`

## Deliverables

- Visual role-instance node IDs that still resolve back to the canonical role.
- Definition-surface role node generation that creates per-step role nodes for repeated role participation.
- Role assignment and decision-authority links routed to the related role instance instead of one distant role hub.
- Recomposition support for role instances near their owning step.
- Repaired default process template coordinates.
- Focused tests and browser proof.

## Dependency Impact

- Process persistence and runtime semantics must remain unchanged: duplicated role nodes are presentation instances, not duplicated role contracts.
- WebGL projection consumes the same definition surface, so it must not drop role instances or place them at the origin.

## Validation Depth

- Critical UI foundation.

## Implementation Steps

1. Add deterministic role-instance node IDs and role-token resolution.
2. Generate per-step role nodes for assignment and decision-authority participation.
3. Route role links to the related role instance.
4. Update recomposition to position role instances from their owning step.
5. Ensure WebGL role layout handles all role nodes from the surface.
6. Regenerate default template canvas coordinates from the recomposition service.
7. Add tests for role duplication, link routing, identity resolution, and representative default-process readability.

## Do Not Do

- Do not duplicate role records in process definitions.
- Do not add a new graph library or change CanvasLib rendering primitives.
- Do not redesign edge routing in this phase.

## Acceptance Checklist

- A role used in multiple steps produces multiple role nodes on the definition surface.
- Each role-instance node resolves to the same canonical role for edit/connection handling.
- Role-binding and decision-role links target the role instance closest to the related step.
- Existing single-role/unbound behavior still works.
- Default template files have clearer saved positions.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "ProcessCanvasSurfaceFactoryTests|ProcessCanvasRecompositionServiceTests|ProcessWebGlSceneAdapterTests"`
- `dotnet build CanDoItAll.slnx`
- Browser proof on `/processes` for a complex default process after recomposition/default-coordinate repair.

## Browser Validation Logging

- Add a `04-role-instance-composition-and-default-template-repair` row to `reviews/01-execution-report.md` with route, viewport, actions, screenshots, and result.

## Progression Gate

- Passed. Role instance rendering, link routing, default template coordinate repair, targeted tests, solution build, and browser evidence were completed.

## Completion Proof

- Targeted tests passed: `19` tests in `ProcessCanvasSurfaceFactoryTests`, `ProcessCanvasRecompositionServiceTests`, and `ProcessWebGlSceneAdapterTests`.
- Module build passed: `dotnet build src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj --no-restore -m:1 -p:BaseOutputPath=.codex\tmp\role-instance-module-bin\`.
- Solution build passed: `dotnet build CanDoItAll.slnx --no-restore -m:1 -p:BaseOutputPath=.codex\tmp\role-instance-solution-bin\` with `0` warnings and `0` errors.
- Browser proof captured on `http://127.0.0.1:5081/processes` after triggering `Recomposition` on `Multi-team software delivery and release governance`.
- Browser analytics: `36` role nodes, `36` role-instance nodes, `39` role-instance links, repeated role titles including `Lead engineer` `9` times and `Delivery manager` `8` times.
- Artifacts: `C:\repositories\CanDoItAll\process-canvas-role-instance-browser-proof.png` and `C:\repositories\CanDoItAll\process-canvas-role-instance-browser-proof.json`.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Keep one canonical role contract in the process model while rendering multiple visual role nodes on the canvas. Prove that links use nearby role instances, default template coordinates are repaired, and persistence/runtime semantics remain unchanged.
```
