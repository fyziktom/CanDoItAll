# 04-scheduler-planner-ui

## Status

- `Completed`

## Objective

- Build the operator-facing Scheduler/Planner page with tabs for active schedules, new schedule setup, and historical scheduled runs.

## Success Criteria

- Own route exists for Scheduler/Planner and is reachable from shell navigation.
- Page has `Scheduled runs`, `New schedule`, and `Run history` tabs.
- Active schedules show current state, target, CRON, CRON description, time zone, next/last fire, recovery/misfire indicators, and actions.
- Active schedules include a CanvasLib `CanvasCalendar` preview of projected/actual scheduled runs.
- New schedule form supports workflow/process target selection, CRON validation, live CRON description preview, timezone, misfire policy, enabled state, start/end windows, and run metadata.
- Run history supports search/filter and shows target run correlation and failure/dead-letter state.
- Browser proof verifies wide and narrower layouts without overlapping text.

## Covered Inputs

- SPM-R001, page visibility side
- SPM-R005, UI display side
- SPM-R006
- SPM-R007
- SPM-R008
- SPM-R010, history rendering side
- SPM-R014
- SPM-R015
- SPM-R017

## Prerequisites

- `01-scheduler-domain-and-persistence` complete.
- `02-quartz-db-recovery-and-fire-dispatch` complete.
- `03-process-and-workflow-run-adapters` complete.
- UI proposal image available at `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module\evidence\ui-layout-proposals.png`.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\Pages\AutomationPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Pages\ProcessesPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ShellNavigation.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Routes.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Calendar\CanvasCalendar.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Calendar\CanvasCalendarContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCanvasSamples.cs`
- `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module\evidence\ui-layout-proposals.png`
- `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module\architecture\01-target-solution.md`

## Deliverables

- SchedulerPlanner page, recommended route `/scheduler` unless implementation chooses `/automation/scheduler` with explicit reasoning.
- Shell navigation item that does not remove or hide existing `/automation`.
- Tabbed UI using existing `Tabs`/workspace patterns.
- Active schedules grid/list with compact operational actions.
- CanvasCalendar preview that maps scheduled plans and actual run history to read-only calendar events.
- New schedule form with typed target selection and live CRON description preview.
- Run history search/filter surface.
- Loading, empty, validation, success, and failure states.
- Component tests and Playwright/browser screenshots.

## Dependency Impact

- Final validation depends on UI proof from this phase.
- UI gaps can conceal backend correctness, so tab state and search/filter behavior must be tested, not only visually inspected.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Review generated UI proposal and existing page patterns.
2. Choose final route and navigation placement; record the decision.
3. Build page shell with `PageScaffold`, `PageHeader`, summary tiles, and `Tabs`.
4. Implement `Scheduled runs` tab with table-first layout, `CanvasCalendar` preview, and detail/drawer or expandable row for next/last fire details.
5. Implement `New schedule` tab with typed target pickers, CRON field, description preview, timezone, misfire, windows, enabled toggle, and save validation.
6. Add a SchedulerPlanner calendar-surface mapper that creates `CanvasCalendarSurface` and read-only `CanvasCalendarEvent` values from projected fires and actual history.
7. Implement `Run history` tab with search, filters, status badges, target run links, and dead-letter/failure indicators.
8. Add component tests for tab switching, validation, save command, active schedule rendering, calendar surface mapping, and history filters.
9. Run browser validation at large maximized viewport and a narrower viewport.
10. Save screenshots and update execution report.

## Scope Exceptions

- Do not redesign existing Automation diagnostics.
- Do not add a marketing landing page or hero page.
- Do not implement backend behavior that belongs to earlier subbundles.
- Do not turn CanvasCalendar into the recurrence editor; use it as visualization/preview over typed schedule projections.

## Do Not Do

- Do not use raw `div`/`span` structures for standard controls when BaseLib/Radzen-style wrappers exist.
- Do not add one-off CSS that bypasses the component system unless no wrapper exists and the execution report explains why.
- Do not hide validation errors or failed target launches.
- Do not use decorative gradients/orbs or card-heavy marketing layout.

## Acceptance Checklist

- `/scheduler` or the chosen route renders.
- All three tabs are keyboard/click reachable.
- New schedule form validates bad CRON and shows a human-readable preview for valid CRON.
- Active schedule list renders next/last fire and status accurately.
- CanvasCalendar renders projected/actual schedule events and selection links back to schedule details.
- History search filters results without layout shift.
- Wide and narrow screenshots show no overlapping text or broken controls.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Component tests for SchedulerPlanner UI.
- Playwright/browser proof for route, all tabs, form validation, active list, and history search.
- CanvasCalendar nonblank rendering proof with scheduled-run events visible.
- Screenshots saved and referenced in `reviews/01-execution-report.md`.

## Browser Validation Logging

- Target route: `/scheduler` or the final chosen SchedulerPlanner route.
- Required viewport passes: maximized desktop/wide and a narrower desktop/tablet-width pass.
- Required actions: navigate route, switch all tabs, inspect CanvasCalendar scheduled-run preview, enter invalid CRON, enter valid CRON, inspect description preview, apply history filter.
- Required evidence: screenshot for each tab in wide viewport, plus at least one narrow-width screenshot.
- Screenshot review questions: Are controls clipped? Does any text overlap? Are target/run statuses scannable? Does the CanvasCalendar preview render nonblank and framed? Does dense data remain usable without a card-heavy layout?

## Progression Gate

- Final validation may start only after component tests pass and screenshots prove all required tabs and validation states render correctly.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Build the Scheduler/Planner page using existing BaseLib/Radzen-style patterns and the prepared service contracts. Validate all three tabs with component tests and browser screenshots, then update the execution report.
```
