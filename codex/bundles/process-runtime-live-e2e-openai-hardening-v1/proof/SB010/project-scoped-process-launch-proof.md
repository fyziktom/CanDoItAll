# SB010 Project-Scoped Process Workspace Proof

## Result
Passed.

## Scope
- Verified `/projects/{projectId}/processes` preserves project context through template import, definition selection, launch-plan creation, and `launchPlanId` query reload.
- Added one focused Playwright test:
  - `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`
- No production code was changed for SB010.

## Assertions Proven
- Project route renders `ProcessWorkspace` with the route `ProjectId`.
- Template import uses the current `ProjectId`.
- The imported process definition is returned from `/api/processes/definitions?projectId=...` with `ProjectId == projectId`.
- Launch-plan creation from the project workspace sends `ProjectId` through `ProcessLaunchCreateRequest`.
- The launch plan is returned from `/api/processes/launch-plans?definitionId=...&projectId=...` with `ProjectId == projectId`.
- Reloading `/projects/{projectId}/processes?processId=...&launchPlanId=...` opens the same project workspace and selected launch plan.

## Validation
- Command:
  `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter FullyQualifiedName~Project_scoped_process_workspace_SB010_INV_001_preserves_project_and_launch_plan_context --logger "trx;LogFileName=SB010-project-scoped-process-launch.trx" --results-directory codex\bundles\process-runtime-live-e2e-openai-hardening-v1\proof\SB010\test-results`
- Result: passed 1 test, 0 failed, 0 skipped.
- Transcript: `bundle://proof/SB010/transcripts/project-scoped-process-launch-playwright.txt`
- TRX: `bundle://proof/SB010/test-results/SB010-project-scoped-process-launch.trx`

## Browser Evidence
- `bundle://proof/SB010/screenshots/01-project-template-selected-large-desktop.png`
- `bundle://proof/SB010/screenshots/02-project-launch-plan-created-large-desktop.png`
- `bundle://proof/SB010/screenshots/03-project-launch-plan-query-large-desktop.png`

## Negative Scans
- Source assertions: `bundle://proof/SB010/transcripts/project-scoped-process-launch-source-assertions.txt`
- Anti-stub/runtime-host drift: `bundle://proof/SB010/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle paths: `bundle://proof/SB010/transcripts/no-transient-bundle-path-scan.txt`
- No unexpected UI/media source drift: `bundle://proof/SB010/transcripts/no-unexpected-ui-media-drift-scan.txt`
- Prepared-stage bundle validator after SB010: `bundle://proof/SB010/transcripts/prepared-validator-after-sb010.txt`
