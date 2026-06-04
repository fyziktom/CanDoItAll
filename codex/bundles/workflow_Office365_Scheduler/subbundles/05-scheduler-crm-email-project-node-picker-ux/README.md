# 05-scheduler-crm-email-project-node-picker-ux

## Status

- Status: `Completed`

## Objective

Make Scheduler Planner practical for Office365 email-watch workflows by rendering typed fields and option-backed pickers instead of forcing raw JSON.

## Covered Inputs

- R8: Scheduler can configure typed input fields for email/contact, project, parent node, processed category, and interval.
- R9: Scheduler can pick email from CRM contacts while still allowing manual email entry.

## Prerequisites

- SB04 schema resolver and required-value validation passed closure.
- Scheduler UI and component test patterns are reviewed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor.css`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModels.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`
- `repo://src/CanDoItAll.Modules.CrmHr/Models/PartyDirectoryManagementModels.cs`
- `repo://src/CanDoItAll.Modules.CrmHr/Services/PartyDirectoryManagementService.cs`
- `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureRuntimeGateway.cs`
- `repo://tests/CanDoItAll.Tests.Components/SchedulerPlannerPageTests.cs`

## Scope

- Add narrow Scheduler workflow input option provider contracts.
- Implement providers for CRM contact email, Office365 connection/category where available, project, and project node options through existing services.
- Render typed Scheduler fields from descriptor schema and synchronize them into `InputJson`.
- Keep raw JSON as an advanced/synchronized editor.
- Add quick interval presets including every two hours with generated Quartz CRON display.

## Dependency Impact

- SB08 depends on this UI to prove the scenario can be configured without hand-writing JSON.
- SB07 may reuse displayed route/status concepts if Scheduler history UI is extended.

## Validation Depth

- Critical component and browser proof for typed form rendering, CRM email selection, manual email entry, project-node dependent options, validation errors, quick interval, raw JSON sync, and responsive layout.
- Browser proof on `/scheduler` desktop and narrow viewport.

## Implementation Steps

1. Add option provider contracts and provider implementations with minimal module coupling.
2. Update Scheduler Planner page to render typed schema fields.
3. Synchronize field values into JSON and preserve advanced raw JSON editing.
4. Add quick interval presets and human-readable CRON summary.
5. Add component tests and browser proof.

## Do Not Do

- Do not make Scheduler directly depend on large module internals when a narrow provider is enough.
- Do not remove raw JSON editing.
- Do not introduce Tailwind unless already used by this page.
- Do not render generic `div`-only controls when existing project components/Radzen patterns are available.

## Acceptance Checklist

- Selecting an Office365 template renders typed Scheduler fields.
- CRM contact picker updates `$.emailAddress`.
- Manual email entry remains possible.
- Project selection loads node options scoped to the project.
- Every-two-hours preset creates a valid Quartz expression.
- Desktop and narrow browser proof shows readable, non-overlapping UI.

## Proof Required

- `bundle://proof/SB05/manifest.md`
- `bundle://proof/SB05/semantic-invariants.md`
- Failing-first transcript for absent typed form.
- Passing component test transcript.
- Browser proof transcript/artifact and screenshots.
- Source assertion and anti-stub audit transcripts.

## Browser Validation Logging

- Route: `/scheduler`.
- Viewports: desktop large viewport first, then narrow/mobile width.
- Actions: select Office365 template, choose or type email, choose project and parent node, select every-two-hours interval, inspect raw JSON sync, trigger validation errors.
- Record screenshots and pass/fail result in `reviews/01-execution-report.md`.

## Progression Gate

- Passed. Browser proof shows the Office365 summary workflow selected in Scheduler, manual email/project/node values synchronized into the expected root JSON properties, and clearing the required email blocks save after removing `emailAddress` from JSON.

## Closure Notes

- Component proof covers CRM email option rendering through `WorkflowInputParameterOptionSourceKind.CrmContacts`.
- Browser proof used local dev data with no CRM contacts, so it proves manual email entry and project/node dependent selectors while preserving the component CRM proof as the option-provider evidence.
- A schedule created during an earlier proof attempt was paused through the UI before final validation; final browser proof shows 0 enabled schedules and 0 upcoming events.

## Suggested Agent Prompt

Implement Scheduler typed input rendering and option-backed pickers for the Office365 email-watch templates, then prove the route with component tests and browser screenshots before proceeding.
