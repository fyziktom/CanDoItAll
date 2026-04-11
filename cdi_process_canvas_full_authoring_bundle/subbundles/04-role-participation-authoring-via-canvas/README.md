# 04-role-participation-authoring-via-canvas

## Status

- `Completed`

## Objective

- Make role participation a first-class canvas-authored relation by exposing role responsibility outputs and step participant inputs, and wiring create and delete actions to the existing canonical role-assignment model.

## Covered Inputs

- `R005` Move toward canvas-primary authoring.
- `R007` Roles must expose participant-role outputs.
- `R010` Support `Responsible`, `Reviewer`, `Approver`, and `Backup` role participation from canvas.
- `R011` Role participation is many-to-many overall.
- `R023` Use Playwright proof with screenshot review.

## Prerequisites

- `subbundles/01-node-inventory-and-port-semantics` must be `Completed` and trusted.
- `subbundles/02-canonical-port-model-and-persistence-foundation` must be `Completed` and trusted.
- `subbundles/03-shared-step-node-multi-port-rendering-and-gesture-parity` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepRoleAssignmentEditor.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`

## Deliverables

- Visible role outputs for `Responsible`, `Reviewer`, `Approver`, and `Backup`.
- Visible participant inputs on step nodes.
- Create and delete behavior that updates `ProcessStepRoleAssignmentRequirement`.
- Focused tests and browser proof for many-role-to-step assignments and reload persistence.

## Dependency Impact

- This is the first generalized authoring feature slice and validates the core assumption that the process canvas can now edit more than structural dependencies and branch routing.
- Weak proof here would undermine confidence in later artifact and routing authoring phases.

## Validation Depth

- `UI, component-test, integration-test, and browser-proof`

## Implementation Steps

1. Extend role-node and step-node projections to show responsibility-specific ports from the typed catalog.
2. Wire connection create and delete handling to role-assignment creation and removal.
3. Preserve many-to-many overall semantics while respecting any uniqueness rules already present in the canonical model.
4. Add focused tests for assignment creation, deletion, reload persistence, and duplicate-rule handling.
5. Prove the flow on `/processes` with visible role-to-step links.

## Scope Exceptions

- This phase does not yet close artifact consumption or all step-contract authoring.
- Decision-authority links that remain distinct from participation links are acceptable as long as the role-participation family is fully generalized.

## Do Not Do

- Do not collapse all participant kinds into one generic `role` input.
- Do not bypass the canonical role-assignment uniqueness rules.
- Do not leave delete behavior behind form-only fallbacks.

## Acceptance Checklist

- Role nodes visibly expose all four responsibility outputs.
- Step nodes visibly expose the corresponding participant inputs.
- Creating a link from a role output to a step participant input creates a canonical assignment.
- Removing the link removes the canonical assignment.
- Multiple roles can target the same participant input family where the canonical model allows it.

## Proof Required

- Focused component-test and integration-test commands.
- Maximized desktop Playwright proof on `/processes`.
- Screenshot showing at least one step with multiple participant-role links.
- Reload proof showing assignments survive refresh.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `Maximized desktop`
- Playwright MCP actions: `navigate`, `create role-to-step participant links`, `reload`, `verify links still exist`, `capture screenshot`
- Screenshot evidence: `proof/screenshots/role-participation-authoring.png`
- Review questions: `Are participant badges readable`, `Do multiple links remain legible`, `Does reload preserve the authored graph`

## Progression Gate

- Step-contract and artifact phases may continue only after role participation works end to end in tests and in the real browser, including reload persistence.

## Suggested Agent Prompt

```text
Implement only subbundle 04 from C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle. Expose responsibility-specific role outputs and step participant inputs, wire create and delete actions to the canonical role-assignment model, add focused tests, prove many-role participation on /processes, and confirm assignments survive reload.
```
