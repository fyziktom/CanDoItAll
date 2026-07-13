# SB12 - API UI Workbench Adoption

## Status

- `Completed`

## Objective

Adopt isolated workflow, template, executor, plugin, and MAF adapter services in the Web API, Blazor workflow UI, workflow canvas editor, executor display adapters, and Workbench project-structure workflow nodes after all backend foundations have passed.

## Success Criteria

- API endpoints use workflow-owned runtime/core/template contracts and do not reach into MAF internals.
- Blazor workflow pages and editor consume workflow/template/executor services through explicit contracts.
- Workbench project-structure workflow nodes consume isolated workflow node/executor services.
- Browser and component proof confirms visible workflows, template selection, executor display, plugin metadata display, and project-structure workflow paths still work.
- API/UI/Workbench failure display consumes typed workflow diagnostics and shows user-safe messages, repair hints, node/executor/plugin/tool context, and no raw secrets.

## Covered Inputs

- R10, R11, R12, R13, R14, R15, R17.
- User AGENTS.md Blazor guidance: keep UI logic predictable, prefer services for non-trivial logic, use existing project component patterns, use Radzen if the project uses it.

## Prerequisites

- SB11 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorDisplayAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowExecutorCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowNodeService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowPreviewSimulationSupport.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureWorkflowInputSettingsNormalizer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.WorkflowNodes.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\AgentTools\ProjectStructureAgentRuntimeToolProvider.cs`

## Deliverables

- API adoption changes with tests for workflow runtime, catalog, template, and run operations.
- Blazor module service adoption without embedding template loading or executor descriptor ownership.
- Workbench workflow node service adoption with project-structure executor/category services.
- Workbench agent-tool workflow add/create/start/status adoption through the same isolated services and access/lease guards.
- Component tests for workflow page/editor state and executor display mapping.
- Component tests for failed workflow/template/executor/plugin display states.
- Browser proof for workflow page and Workbench workflow-node success and failure paths.

## Dependency Impact

- SB13 hardens adoption and checks for hidden fallback paths. SB14 final regression depends on API/UI/Workbench behavior being validated after backend isolation.

## Validation Depth

- `UI, API, component-test, and browser-proof`
- Unit, integration, component, Playwright/browser, and visual inspection proof.

## Implementation Steps

1. Update API endpoints to use isolated workflow runtime/core/template contracts.
2. Update Blazor workflow page/editor services to consume template/executor catalog/display abstractions.
3. Keep UI logic focused on rendering/orchestration; move non-trivial logic to services.
4. Update Workbench project-structure workflow node services to consume isolated project-structure executor and workflow contracts.
5. Update Workbench agent runtime workflow tools to use the same adopted service boundary.
6. Add or adjust component tests for template list, executor catalog display, plugin metadata display, canvas node creation, validation messages, and typed diagnostic display.
7. Run API/integration tests for workflow operations.
8. Run browser validation for workflow page and Workbench workflow-node success and failure scenarios with screenshots and DOM checks.

## Scope Exceptions

- This subbundle should not redesign UI styling or navigation unless required by service adoption.
- Final cleanup of obsolete files is SB14 after SB13 hardening.

## Do Not Do

- Do not move UI-only concerns into workflow core/runtime projects.
- Do not use MAF internals from UI/API/Workbench as a fallback.
- Do not introduce new Tailwind/Radzen patterns unless already consistent with the project.
- Do not broaden this into unrelated Blazor refactoring.
- Do not parse exception strings in UI/API code to recover node, executor, plugin, tool, or repair context.
- Do not display raw secrets, tokens, provider payloads, file contents, or host-command sensitive arguments.

## Acceptance Checklist

- [x] API workflow tests pass through isolated services.
- [x] Blazor workflow UI component tests pass.
- [x] Workbench workflow-node tests pass.
- [x] Workbench agent-tool workflow add/create/start/status tests or integration proof pass.
- [x] Browser proof covers workflow page and Workbench workflow-node path.
- [x] Executor and plugin descriptor display remains compatible.
- [x] Failed workflow/executor/plugin/template states show user-safe repairable diagnostics.
- [x] No UI/API/Workbench code references old MAF workflow internals.

## Proof Required

- `proof/SB12/manifest.md` with changed file hashes, API/component/browser transcripts, screenshots, and DOM assertion notes.
- `proof/SB12/semantic-invariants.md` covering API contract compatibility, UI state behavior, executor display parity, plugin metadata display, Workbench node behavior, typed explicit errors, redaction, repair hints, and no MAF fallback.
- Semantic Adequacy Gate proof with adversarial invalid workflow/template/executor UI cases, positive workflow and Workbench browser cases, and anti-stub audit.

## Completion Evidence

- Proof manifest: `bundle://proof/SB12/manifest.md`.
- Semantic invariants: `bundle://proof/SB12/semantic-invariants.md`.
- Unit proof: `bundle://proof/SB12/transcripts/unit-diagnostics-tests.txt` passed 13/13.
- Component proof: `bundle://proof/SB12/transcripts/component-workflows-page-tests.txt` passed 21/21.
- Integration proof: `bundle://proof/SB12/transcripts/api-workbench-integration-tests.txt` passed 14/14.
- Browser proof: `bundle://proof/SB12/transcripts/playwright-workflow-shell-large.txt` and `bundle://proof/SB12/transcripts/playwright-workbench-workflow-node-large.txt` each passed 1/1 with screenshots under `bundle://proof/SB12/browser/`.
- Static proof: `bundle://proof/SB12/transcripts/static-adoption-check.txt`, `bundle://proof/SB12/transcripts/semantic-source-assertions.txt`, `bundle://proof/SB12/transcripts/adversarial-negative-check.txt`, and `bundle://proof/SB12/transcripts/anti-stub-audit.txt`.
- Workbook proof: `bundle://proof/SB12/transcripts/workbook-update-and-render.txt` with rendered previews under `bundle://proof/SB12/workbook-previews/`.

## Browser Validation Logging

- Required routes:
  - Workflow page route used by `WorkflowsPage`.
  - Workbench project-structure page route with workflow-node interaction.
- Required viewport passes:
  - Maximized large-screen pass only.
  - Small and medium viewport tests are intentionally skipped because the app is large-screen-only for this initiative.
- Required Playwright actions:
  - Open workflow page.
  - Verify templates load.
  - Verify executor catalog/display includes default and plugin metadata when fixture data is available.
  - Trigger or load a failed workflow/template/executor fixture and verify diagnostic message, repair hint, node/executor/plugin/tool context, and secret masking.
  - Create or inspect a workflow node without console errors.
  - Open Workbench project-structure workflow path and verify node settings behavior.
- Evidence:
  - Screenshot paths under `proof/SB12/browser/`.
  - Console error log.
  - DOM assertion transcript.
- Review questions:
  - Does text fit within workflow/editor controls?
  - Are executor/plugin labels visible and not duplicated?
  - Does the canvas remain usable at the tested viewport?

## Progression Gate

- SB13 cannot start until API, component, Workbench, and browser proof show adoption through isolated services with no hidden MAF fallback references.

## Suggested Agent Prompt

```text
Implement SB12 only. Adopt the isolated workflow/template/executor/plugin/MAF adapter services in API, Blazor workflow UI, workflow canvas editor, executor display, and Workbench workflow nodes. Keep UI logic predictable, add API/component/browser success and failure-diagnostic proof, and capture Semantic Adequacy Gate evidence. Do not redesign unrelated UI or perform final cleanup.
```
