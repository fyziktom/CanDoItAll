# Workflow Canvas Editor And Component Library

## Status

- `Ready`

## Objective

- Add a workflow-specific canvas editor and prepared component library UI that lets users compose workflows from typed nodes such as LLM call, triage, strict logic, artifact, human input, agent step, subworkflow, start, and end.
- Reuse CanvasLib and process-canvas interaction patterns without making process definitions the workflow model.

## Success Criteria

- Workflow graph editing uses strongly typed workflow node/edge/component models.
- Prepared LLM Call Components can be browsed, selected, configured, and placed onto the canvas.
- Canvas validation catches graph, port, shape, component, and provider/model issues before test execution.
- The canvas can build at least one realistic LLM-call workflow and submit it to validation/test-run APIs.
- Canvas supports durable-friendly workflow patterns from MAF where in scope: fan-out/fan-in, conditional routing, RequestPort/human input, agent executors, and sub-workflow nodes.
- Browser proof shows usable large-screen canvas and acceptable narrower-width behavior.

## Covered Inputs

- RQ-010, RQ-013, RQ-014, RQ-015, RQ-016, RQ-020, RQ-021.
- RN-007, RN-008, RN-010, RN-011, RN-012, RN-013.

## Prerequisites

- Subbundle 03 completed for workflow catalog, component library, validation, and test-run APIs.
- Subbundle 04 completed for Workflows page route/state.
- CanvasLib and process canvas patterns have been reviewed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\CanDoItAll.Components.CanvasLib.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Canvas`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Actions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Artifacts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Editor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Persistence.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolbarActions.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasToolboxWindow.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Workflows\Visualization\WorkflowVisualizer.cs`

## Deliverables

- Workflow canvas state model and UI components under the Agents module or shared AgentFramework components as approved by prior reviews.
- Workflow toolbox/component library panel showing prepared LLM Call Components and other node types.
- Node editors for LLM call, strict logic, triage, human input, agent step, artifact output, subworkflow, start, and end nodes.
- Edge/port editing with typed source/target ports and validation feedback.
- Canvas save/load/version integration with workflow catalog APIs.
- Validate/test action that calls workflow validation/test-run APIs and surfaces structured errors/results.
- Optional Mermaid/DOT preview or debug export based on MAF visualization support if useful and small.
- Browser proof for layout, interactions, and screenshot review.

## Dependency Impact

- Subbundle 07 depends on canvas integration for app-level route/navigation polish.
- Subbundle 08 depends on canvas proof for end-to-end workflow authoring.
- Process integration does not depend on canvas editing directly, but workflow definitions created here must be runnable by subbundle 06.

## Validation Depth

- Critical UI foundation with component, service/API, and browser-proof depth.
- Architecture review required for canvas model separation and component library ergonomics.

## Implementation Steps

1. Inspect CanvasLib and process canvas code to identify reusable primitives and patterns.
2. Add workflow canvas state models around workflow domain models, not process canvas models.
3. Implement toolbox/component library UI for prepared LLM Call Components and fixed node kinds.
4. Implement node/edge editing and validation state display.
5. Add node configuration editors with typed fields and explicit validation messages.
6. Wire save/load/validate/test actions to workflow APIs.
7. Add tests for graph-to-definition mapping, validation display, component insertion, and node/edge editing where project patterns allow.
8. Run build/tests.
9. Run browser validation on maximized desktop and narrower width.
10. Run architecture review for canvas/domain separation.
11. Update execution report.

## Scope Exceptions

- Do not redesign the process canvas.
- Do not implement process role workflow selection here.
- Do not require full declarative YAML import/export unless it is already approved as a small addition by earlier architecture review.

## Do Not Do

- Do not store arbitrary graph JSON as the only workflow definition.
- Do not copy process canvas models and rename them without preserving workflow-specific semantics.
- Do not allow node kinds or port names as unvalidated magic strings.
- Do not add a visually decorative canvas shell that lacks real edit/test behavior.

## Acceptance Checklist

- Canvas edits typed workflow definitions.
- Toolbox includes prepared LLM Call Component and required workflow node kinds.
- Node editors expose provider/model/modality/settings/instructions/result-shape configuration where relevant.
- Node/edge model can represent RequestPort and sub-workflow semantics needed by durable execution.
- Validation catches graph and component errors before runtime.
- Test-run action can execute a valid workflow or show structured failure.
- Browser screenshots show usable layout and no overlapping/clipped controls.
- Architecture review accepts workflow canvas separation from process canvas.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Component/service tests for canvas mapping and validation where available.
- Browser screenshot of maximized desktop canvas with toolbox, selected node editor, and validation/test panel.
- Browser screenshot of narrower-width behavior.
- Execution report route, viewport, Playwright actions/assertions, screenshots, and visual review notes.

## Browser Validation Logging

- Route: Workflows page route with canvas/editor view.
- Viewports: maximized desktop and one narrower-width pass.
- Playwright evidence: navigate, open canvas, add LLM Call Component, edit instructions/result shape, connect nodes, validate, run test or inspect validation output.
- Screenshots: desktop canvas, selected node editor, validation/test output, narrower-width layout.
- Review questions: verify canvas is not blank, controls are stable, text fits, node/edge interactions work, validation is visible, and component choices are understandable without explanatory marketing text.

## Progression Gate

- App integration and final validation may proceed only after a valid workflow can be composed or loaded through the canvas, validated, and either test-run or rejected with structured validation errors.

## Suggested Agent Prompt

```text
Implement subbundle 05 only.
Build workflow canvas editing and prepared component library UI on top of workflow models/APIs.
Use CanvasLib and existing process canvas patterns as references, but do not reuse process definitions as the workflow model.
Capture browser proof and update reviews/01-execution-report.md.
```
