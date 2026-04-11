# 03-shared-step-node-multi-port-rendering-and-gesture-parity

## Status

- `Ready`

## Objective

- Generalize the shared advanced-node rendering and interaction path so process steps can expose multiple badge-anchored ports with the same stability, gesture behavior, and zoom fidelity already expected from the branch-router work.

## Covered Inputs

- `R006` Steps must gain explicit structural and participation semantics on canvas.
- `R008` Branch routers stay additive but generalized.
- `R023` Use Playwright proof with screenshot review.

## Prerequisites

- `subbundles/01-node-inventory-and-port-semantics` must be `Completed` and trusted.
- `subbundles/02-canonical-port-model-and-persistence-foundation` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchNode.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchPortGeometry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchAnchorPorts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\02-layout-and-legacy-render.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07a-runtime-interaction-router.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\css\workbench\overlays\05-overlays-and-composer.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`

## Deliverables

- Shared rendering support for step nodes with multiple badge-aligned input and output ports.
- Shared gesture behavior that still honors the current left-click connector authoring flow.
- Zoom-stable badge anchoring for the richer step node surfaces.
- Focused component or renderer tests plus browser smoke on `/processes`.

## Dependency Impact

- Role-participation authoring and step-contract authoring both depend on this phase.
- Weak proof here would let later features appear broken when the real defect is still shared rendering or hit-testing.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Generalize the advanced-node rendering path so step nodes can render grouped badges and ports using the typed process-canvas catalog.
2. Keep connector anchors badge-relative and zoom-stable.
3. Preserve the existing left-click connection authoring flow and legacy fallback behavior.
4. Add focused tests for rendered port geometry, anchor stability, and interaction routing.
5. Run a dependent browser smoke on `/processes` and capture screenshots before closing the phase.

## Scope Exceptions

- This phase does not yet need to implement all role and artifact authoring handlers.
- This phase is about shared rendering and interaction parity, not full business-meaning closure.

## Do Not Do

- Do not hardcode process-specific semantics directly into CanvasLib.
- Do not skip screenshot review just because tests pass.
- Do not regress the earlier branch-router advanced node behavior.

## Acceptance Checklist

- Step nodes can render multiple visible ports aligned to their badges.
- Badge anchors remain stable under zoom changes.
- Left-click source and target interaction still works with the richer node surfaces.
- Legacy nodes still render correctly.

## Proof Required

- Focused component or renderer test command.
- Browser smoke on `/processes` in a maximized desktop viewport.
- At least one close-up screenshot showing badge-aligned step ports.
- Narrower-width follow-up if added badges or pills wrap.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `Maximized desktop`, then `narrower follow-up if layout changed`
- Playwright MCP actions: `navigate`, `select relevant step node`, `start and cancel one draft connection`, `inspect visible badge anchors`, `capture close-up screenshot`
- Screenshot evidence: `proof/screenshots/step-multi-port-desktop.png`, optional narrow screenshot
- Review questions: `Are badge labels readable`, `Are circles centered on the correct badges`, `Do anchors drift when zoom changes`, `Is anything overlapping or clipped`

## Progression Gate

- Downstream authoring phases may continue only after shared tests pass and the real `/processes` browser smoke shows stable, readable, badge-aligned step ports with no obvious regressions.

## Suggested Agent Prompt

```text
Implement only subbundle 03 from C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle. Generalize the shared advanced-node rendering and interaction path so process steps can expose multiple badge-aligned ports, keep anchors zoom-stable, preserve the current left-click connection flow, add focused shared tests, and prove the result on /processes before moving on.
```
