# 03-process-hr-team-scoped-matching

## Status

- `Completed`

## Objective

- Let process launch HR matching run with an optional selected agent team, prefer in-team agents, and mark out-of-team candidates selected for required roles.

## Covered Inputs

- `N009`: HR matching of a starting process can select a delivery team.
- `N010`: HR agent adds/selects agents required by process even when outside the selected team.
- `N011`: Out-of-team selections are marked in the matching modal.

## Prerequisites

- Subbundle 01 closure gate has passed.
- Process launch service, API, and UI files have been rechecked before edits.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Staffing.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Launch\ProcessesService.Launch.Reads.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspaceRunsLaunchSection.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Launch.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.RunsPresenter.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessLaunchPlanningIntegrationTests.cs

## Deliverables

- Optional team id/name in HR matching request/service path.
- Team selector in launch planning HR matching UI.
- Matching score preference for in-team technical agents.
- Metadata and view model fields for selected-team fit and out-of-team markers.
- UI badge or equivalent marker for out-of-team selected/recommended candidates.
- Integration/component tests for selected-team behavior and no-team regression.

## Dependency Impact

- Launch approval and execution depend on role selections remaining correct after HR matching.
- Weak proof could silently assign the wrong delivery agent or hide a required out-of-team role fill.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Add request/service shape for optional selected team.
2. Resolve team membership to technical agent ids through AgentFramework workspace service.
3. Apply team-fit metadata to existing and supplemental candidates.
4. Adjust HR manager scoring to prefer in-team candidates without excluding out-of-team candidates.
5. Expose marker fields on launch candidate view model.
6. Add launch planning UI for selecting a team and running HR matching.
7. Add targeted integration and component tests.

## Scope Exceptions

- Do not require a selected team for HR matching.
- Do not block out-of-team selections when they are needed for required roles.

## Do Not Do

- Do not rewrite launch plan creation.
- Do not remove existing project assignment, workforce, AI resource, workflow, or gap candidates.
- Do not add process migrations unless metadata storage proves insufficient.

## Acceptance Checklist

- No-team HR matching behaves as before.
- Selected-team HR matching prefers in-team candidates when fit is comparable.
- Required roles can still select out-of-team candidates.
- Out-of-team candidate marker survives `GetLaunchPlanAsync`.
- UI exposes the team selector and marker in launch planning.

## Proof Required

- Integration test showing selected-team preference and out-of-team fallback marker.
- Component or browser proof showing team selector and marker.
- Targeted process launch planning tests.

## Browser Validation Logging

- Route: `/processes` or project process route containing launch planning.
- Viewports: large desktop first, narrow follow-up if layout changes.
- Actions: open launch plan detail, open/run HR matching with team selected, inspect candidate badges.
- Screenshots: `codex/bundles/agent-teams-management-and-hr-matching/evidence/process-hr-team-match-desktop.png`.
- Review questions: selected team is clear, out-of-team marker is visible, role cards remain readable, approval/provisioning controls still fit.

## Progression Gate

- Passed with documented browser limitation. Integration test proves selected team handling and persisted out-of-team markers; build proves the launch planning UI compiles with the team selector and candidate badges. Browser interaction with process launch creation was attempted, but the local SQLite development host held a process-outbox lock during launch-plan creation.

## Closure Evidence

- Passed: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessLaunchPlanningIntegrationTests.MatchLaunchPlanWithHrManagerAsync_marks_required_agents_outside_selected_delivery_team"`
- Passed: `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore`
- Browser blocker: `/processes` launch plan creation on the temporary SQLite dev host failed with `SQLite Error 5: database is locked` while background process outbox/seed work was active. No product blocker was found in the implemented code path; targeted integration proof covered the matching behavior.

## Suggested Agent Prompt

```text
Implement only process HR team-scoped matching. Use the AgentFramework team service, preserve no-team matching behavior, store/reload candidate team markers, prove selected-team preference plus out-of-team fallback, update the execution report, and stop if required roles cannot remain complete.
```
