# 05-step-contract-artifact-and-routing-authoring

## Status

- `Completed`

## Objective

- Complete generalized step authoring on the canvas by exposing structural step ports, generalized routing behavior, and artifact contract ports, with honest canonical handling for artifact consumption.

## Covered Inputs

- `R005` Move toward canvas-primary authoring.
- `R006` Steps must gain explicit structural and participation semantics.
- `R008` Branch routers stay additive but generalized.
- `R012` Decision authority remains singular on the target side.
- `R013` Structural dependencies preserve many-upstream joins and downstream fan-out.
- `R014` Branch outcome routing remains explicit per outcome.
- `R015` Decide whether artifact expectations require explicit graph links.
- `R016` Extend canonical storage if explicit artifact consumption is needed.
- `R017` Classify artifact ports by cardinality and grouping.
- `R023` Use Playwright proof with screenshot review.

## Prerequisites

- `subbundles/01-node-inventory-and-port-semantics` must be `Completed` and trusted.
- `subbundles/02-canonical-port-model-and-persistence-foundation` must be `Completed` and trusted.
- `subbundles/03-shared-step-node-multi-port-rendering-and-gesture-parity` must be `Completed` and trusted.
- `subbundles/04-role-participation-authoring-via-canvas` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessArtifactExpectationEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasBranching.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`

## Deliverables

- Visible structural input and output ports on step nodes.
- Visible artifact-output and artifact-input contract ports following the canonical design settled in subbundle 02.
- Generalized connection authoring for direct dependencies, routed dependencies, and artifact links where in scope.
- Step-kind-aware port applicability rules reflected in the UI.
- Focused tests and browser proof for joins, fan-out, routing, and artifact-related authoring.

## Dependency Impact

- Final scenario proof depends on this phase because it closes the main gap between branch-only authoring and full process-graph authoring.
- Weak proof here would let final scenario demos pass only because they avoid the hard parts of step authoring.

## Validation Depth

- `Process-critical UI, component-test, integration-test, and browser-proof`

## Implementation Steps

1. Project structural step ports and any artifact contract ports from the typed catalog.
2. Wire create and delete behavior for direct dependencies, routed dependencies, and artifact relations in scope.
3. Enforce visible step-kind applicability rules so `Start`, `Decision`, and `End` behave intentionally.
4. Add focused tests for many-upstream joins, downstream fan-out, branch-outcome routing, and artifact-link round-trips where applicable.
5. Prove the resulting authoring flow on `/processes` with close-up screenshots and reload confirmation.

## Scope Exceptions

- If artifact consumption is explicitly blocked by a canonical decision from subbundle 02, list the exact blocked relation here and keep it visible through final closure.
- Runtime projection is not closed here; it lands in subbundle 06.

## Do Not Do

- Do not reintroduce generic node-body connections for relations that now have explicit ports.
- Do not hide step-kind rules in invisible heuristics without matching visible port behavior.
- Do not treat artifact-output badges as complete if downstream consumption still cannot be authored honestly.

## Acceptance Checklist

- Step nodes expose visible structural ports.
- Many-upstream joins and downstream fan-out can be authored from the canvas.
- Branch-router flows continue to work with the generalized step contract.
- Artifact-related authoring matches the canonical decision from subbundle 02.
- Reload preserves the authored step graph.

## Proof Required

- Focused component-test and integration-test commands.
- Maximized desktop Playwright walkthrough on `/processes`.
- Close-up screenshots showing structural and artifact ports.
- Reload-round-trip proof for newly authored links.
- Narrower-width pass if added contract badges wrap or compress.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `Maximized desktop`, then narrower follow-up if layout changes
- Playwright MCP actions: `navigate`, `author step dependencies`, `author routed dependency`, `author artifact link if in scope`, `reload`, `verify graph persists`, `capture screenshots`
- Screenshot evidence: `proof/screenshots/step-contract-authoring-desktop.png`, `proof/screenshots/step-contract-closeup.png`
- Review questions: `Are structural and artifact badges readable`, `Do joins remain understandable`, `Do ports stay aligned under zoom`, `Is the graph still legible after links are added`

## Progression Gate

- Final closure may continue only after generalized step authoring works in tests and in the browser, and any remaining exception is explicit, small, and honestly documented.

## Suggested Agent Prompt

```text
Implement only subbundle 05 from C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle. Complete generalized step authoring on the canvas with structural ports, artifact contract ports, and generalized routing behavior, respect the canonical decisions from earlier subbundles, add focused tests, and prove joins, fan-out, reload persistence, and screenshot readability on /processes.
```
