# Workflow Canvas Routing Authoring UX

## Status

- `Ready`

## Objective

- Add workflow-canvas authoring support for the new basic routing contract so users can create, edit, visualize, validate, save, and preview-run direct, IF predicate, switch case/default, and fan-out routes.
- Replace the current free-text `ConditionExpression` edge editor as the primary authoring surface while keeping legacy text visible where useful.
- Make route state understandable directly on the canvas and in the edge inspector without requiring users to write raw JSON or code for normal IF/SWITCH cases.

## Success Criteria

- The edge editor exposes a route mode selector and typed controls for predicate, switch, default, and fan-out routing.
- Canvas links or edge rows show useful route labels/summaries so branch meaning is visible without opening every edge.
- New edge drafts map to/from `WorkflowDefinition` with `WorkflowEdge.Routing` intact.
- Validation issues are surfaced near the selected edge and in the existing workflow validation area.
- Browser proof shows route creation, editing, validation, save, and preview-run on a maximized desktop viewport plus at least one narrower-width pass.

## Covered Inputs

- User requirement: add basic routing into the workflow canvas UI.
- Current-state finding: `WorkflowCanvasEdgeDraft` has `Kind` and free-text `ConditionExpression` only.
- Current-state finding: `BuildSurface` currently projects links with `Kind = edge.Kind.ToString()` and no route-specific label/summary/tone.
- Current-state finding: the Razor edge inspector currently edits source, target, kind, and condition text.
- Architecture requirement: route builder should use deterministic built-in JSON route fields, not raw executable expressions.

## Prerequisites

- Subbundle 01 completed route domain contract.
- Subbundle 02 completed runtime/compiler proof so UI does not author non-executable metadata.
- Existing workflow canvas tests are passing before UI changes begin.

## Exact Source References

- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasModels.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.css`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.Components.CanvasLib/Canvas/Graph/Primitives/ConnectorPathPrimitive.cs`
- `/mnt/data/cando/CanDoItAll-agents-integration/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `/mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Components/WorkflowsPageTests.cs`

## Deliverables

- Updated `WorkflowCanvasEdgeDraft` carrying `WorkflowEdgeRouting` and compatibility `ConditionExpression`.
- Mapper updates so `FromDefinition`, `ToDefinition`, and `BuildSurface` round-trip and display route metadata.
- Edge inspector route builder with mode-specific fields and summaries.
- Optional shared canvas link additions such as `Label`, `Summary`, `Tone`, or `Badge` if they can be introduced without breaking existing process canvas consumers.
- Component tests for route authoring, route summary rendering, mapping to workflow definition, and validation display.
- Browser evidence for route creation/edit/save/preview-run.

## Dependency Impact

- Subbundle 04 depends on UI-generated definitions to test API/persistence round-trip realistically.
- Subbundle 05 final closure depends on browser screenshots and UI proof from this subbundle.
- Any regression in shared CanvasLib can affect process canvas; tests or manual browser review must explicitly check for shared-link compatibility.

## Validation Depth

- `Critical UI foundation`: component tests plus maximized browser proof are required because the user explicitly called out workflows canvas support.

## Implementation Steps

1. Add routing fields to `WorkflowCanvasEdgeDraft` and default new edges to `WorkflowEdgeRouting.Always`.
2. Update `WorkflowCanvasDefinitionMapper.FromDefinition` and `ToDefinition` to preserve `Routing` and legacy `ConditionExpression`.
3. Add helper methods to summarize routes, choose badge text, and map invalid/incomplete route state into existing validation display.
4. Replace the primary condition text area in `WorkflowCanvasEditor.razor` with a route-mode section: Direct, If predicate, Switch case, Switch default, Fan-out selector.
5. Add typed controls for JSON path, operator, expected value kind, expected value, case sensitivity, fan-out target index/order, and route label.
6. Keep a collapsible or read-only legacy condition field only when existing data is present or project product requirements require it.
7. Extend canvas link display with route labels/summaries if low risk; otherwise show route summaries in edge list and inspector first, and leave shared connector changes for a small follow-up patch.
8. Add CSS for compact route controls, badges, validation states, and responsive wrapping.
9. Add component tests that create/edit a predicate route, switch default route, and fan-out route from the canvas model.
10. Run component tests and targeted unit tests, then browser proof with screenshots.

## Scope Exceptions

- Do not implement ARTL text editor or custom DSL authoring here.
- Do not require full JSONPath editor/autocomplete unless it is trivial and tested.
- Do not redesign the whole workflow canvas or node authoring system.
- Do not change process canvas visuals unless a shared CanvasLib route-label addition requires a safe, tested default.

## Do Not Do

- Do not ask users to type C# predicates, JavaScript, or opaque script snippets.
- Do not hide route state solely in an advanced JSON blob.
- Do not show a route as valid in the UI when the validator/compiler would reject it.
- Do not create shared canvas link changes without checking process-canvas compatibility.

## Acceptance Checklist

- Users can author a Direct route without extra required fields.
- Users can author an IF predicate route with JSON path/operator/value controls.
- Users can author switch case and switch default routes from one source node.
- Users can author fan-out target routes with clear order/index behavior.
- Route summary appears in the edge inspector and either on the connector or in the edge list.
- Save/load preserves route metadata.
- Preview run honors route choices through subbundle 02 compiler behavior.
- Browser screenshots show no clipped, overlapping, or unreadable controls.

## Proof Required

- `dotnet test /mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter FullyQualifiedName~WorkflowsPageTests --verbosity minimal -m:1`
- `dotnet test /mnt/data/cando/CanDoItAll-agents-integration/tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~WorkflowFoundationTests|FullyQualifiedName~WorkflowExecutorTests" --verbosity minimal -m:1`
- Browser proof route: `/agents/workflows` or the current workflow canvas route.
- Screenshots: maximized route-builder edge inspector, canvas with route labels/summaries, validation failure for incomplete route, successful preview-run for a predicate/switch workflow, narrower-width route-builder layout.

## Browser Validation Logging

- Route: workflow canvas/editor page reached from the Agents workflow area.
- Viewports: maximized desktop first; one narrower-width pass after the desktop proof passes.
- Playwright actions/assertions: open canvas, add or select route edges, set IF predicate fields, set switch default, save, validate, run preview, assert visible branch result or validation message.
- Evidence files: `reviews/evidence/subbundle-03/workflow-routing-canvas-desktop.png`, `workflow-routing-edge-inspector.png`, `workflow-routing-validation.png`, `workflow-routing-preview-run.png`, and `workflow-routing-narrow.png`.
- Screenshot review questions: are route controls readable, are route summaries understandable, are invalid states obvious, does the canvas avoid overlap/clipping, and can a user tell which branch is default?

## Progression Gate

- Subbundle 04 and 05 UI closure may proceed only after a canvas-authored route survives save/load and either preview-runs correctly or fails with a structured route validation error.

## Suggested Agent Prompt

```text
Implement subbundle 03 only.
Add workflow canvas routing authoring for WorkflowEdge.Routing, preserve legacy ConditionExpression, map route summaries into the canvas/edge inspector, write component tests, and capture maximized plus narrower-width browser proof. Do not implement ARTL.
```
