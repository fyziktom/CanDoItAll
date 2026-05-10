# Agents Module Workflows Page

## Status

- `Ready`

## Objective

- Add a separate Workflows page inside the existing Agents module for workflow catalog, details, settings, test runs, run history, artifacts, and human-in-loop requests.
- Keep the page focused on workflow orchestration and avoid overloading the existing Agents page.

## Success Criteria

- Users can navigate to a workflow-specific page in the Agents module.
- The page shows workflow catalog/list, selected workflow detail, settings summary, validation state, test-run controls/results, run timeline, artifacts, and pending human-in-loop requests.
- The UI uses existing module/component patterns and is browser-verified at large and narrower widths.
- Architecture review approves the page boundary before canvas and process integration depend on it.

## Covered Inputs

- RQ-008, RQ-009, RQ-011, RQ-012, RQ-019, RQ-020, RQ-021.
- RN-006, RN-007, RN-008, RN-009, RN-011.

## Prerequisites

- Subbundle 03 completed for catalog/settings/test APIs.
- Subbundle 02 completed for run history, artifacts, and external requests.
- Existing Agents module patterns have been reviewed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDiagnosticsPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\ScenarioHarnessPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\CanDoItAll.AgentFramework.Components.csproj`

## Deliverables

- New workflow page/route under `CanDoItAll.Modules.AgentFramework` with page state isolated from `AgentsHomePage`.
- Workflow catalog panel or page section.
- Workflow detail/settings summary panel.
- Workflow validation/test-run panel using subbundle 03 APIs.
- Workflow run history/timeline panel using subbundle 02 runtime projections.
- Artifact list/detail UI for workflow artifacts.
- Pending RequestPort/human-in-loop request UI with explicit response/cancel behavior where runtime supports it, aligned with DurableTask response/status semantics when that backend is selected.
- Browser proof and screenshot review notes.

## Dependency Impact

- Subbundle 05 can reuse page layout and state patterns for the workflow canvas/editor.
- Subbundle 07 depends on this route for navigation/app integration.
- Subbundle 08 depends on page proof for final end-to-end validation.

## Validation Depth

- UI, API integration, component-state, and browser-proof depth.
- Architecture review required for UI/domain separation.

## Implementation Steps

1. Inspect existing Agents page and components for module conventions.
2. Add a separate Workflows page route and page state.
3. Add workflow catalog/detail/settings/test-run/run-history/artifact/request sections using existing component patterns.
4. Wire data loading and mutation through workflow APIs/services from subbundle 03.
5. Add explicit loading, empty, validation-error, runtime-error, pending-request, and completed states.
6. Add component tests if the project has component testing patterns.
7. Run build/tests.
8. Start the app or use the existing dev loop.
9. Run browser validation on maximized desktop and narrower width.
10. Run architecture review for page boundary and UI state handling.
11. Update execution report with screenshots and review notes.

## Scope Exceptions

- Full node-based workflow canvas editing belongs to subbundle 05.
- Process role selection belongs to subbundle 06.

## Do Not Do

- Do not merge workflows into the existing Agents page as another tab unless architecture review explicitly accepts that, because the user asked for its own page.
- Do not build UI by duplicating large process components without extracting reusable wrappers.
- Do not put non-trivial workflow logic directly in Razor markup when a service or page model is more testable.
- Do not use Tailwind unless the touched project already uses it.

## Acceptance Checklist

- Workflows route/page exists inside Agents module.
- Catalog/detail/settings/test-run/run-history/artifact/request states render.
- Runtime/API errors are visible and actionable.
- Pending durable RequestPort requests can be inspected and responded to through product UI/API when runtime exposes them.
- Page does not depend on process definition models.
- Browser screenshots show no overlap, clipped text, or broken responsive behavior.
- Architecture review accepts UI state separation.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Relevant module/component test command if available.
- Browser validation with maximized desktop screenshot.
- Browser validation with narrower-width screenshot.
- Execution report entries with route, viewport, Playwright actions/assertions, screenshots, and visual review notes.

## Browser Validation Logging

- Route: new Workflows page route under Agents module, exact route recorded by implementation agent.
- Viewports: maximized desktop and at least one narrower-width pass.
- Playwright evidence: navigate, load catalog, select workflow, open settings/test/run history, inspect artifacts/request states.
- Screenshots: save workflow page desktop and narrower-width screenshots and record paths.
- Review questions: verify page identity is clearly Workflows, text does not overlap, actions are discoverable, runtime errors are actionable, and empty states are not misleading.

## Progression Gate

- Canvas and app navigation integration may proceed only after the Workflows page route renders real workflow data or controlled empty states and browser proof passes.

## Suggested Agent Prompt

```text
Implement subbundle 04 only.
Add a separate Workflows page in the existing Agents module using workflow APIs and runtime projections.
Do not implement the canvas editor or process role integration.
Capture browser screenshots and update reviews/01-execution-report.md.
```
