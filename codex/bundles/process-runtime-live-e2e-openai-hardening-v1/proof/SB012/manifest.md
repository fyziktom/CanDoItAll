# SB012 Manifest

## Status
Passed.

## Scope
Gate D project-structure launch closure proves that executed project-structure process runs keep project/node/source context and that projected run output folders can navigate to the exact project-scoped process run.

## Changed Files
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Launch.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessesPage.razor`
- `src/CanDoItAll.Modules.Processes/Pages/ProjectProcessesPage.razor`
- `src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAssemblyService.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectStructureAgentApiIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`

## Changed-File Hashes
See `proof/SB012/changed-file-hashes.txt`.

Key hashes:
- `ProcessWorkspace.Launch.cs`: `0AA3B29DC9E0C272B577FEAE73C51BA770358D90B47BD2018367DB6F29567E4C`
- `ProcessWorkspace.razor.cs`: `1372D4AED806C00E8EFD55D3AF4C8981229132B9C602188464E70B1A7CCB1577`
- `ProcessesPage.razor`: `46028716F9B68AB8EACAB31D2EB5E59B809D6B7E80421D8B3623A542914E9FCA`
- `ProjectProcessesPage.razor`: `8D71F848C8734A709AD653D1C3F11FCF971CA375E9C17D3AC9425725F48605F5`
- `ProjectStructureAssemblyService.cs`: `C60460E2CDEC5CD7675392D285165082B0E7280963C7EA3315240AC04DB1626E`
- `ProjectStructureAgentApiIntegrationTests.cs`: `D4D9865A959A49607D48E3A16671A58DAA6F6EEDF222C2E2566DD115D6D3D2BC`
- `ProjectWorkbenchServiceIntegrationTests.cs`: `2DFF4B7702CCD24329075D7271B1EA5E1B6372C69D76B1B0A6C06BE3C8323C61`
- `AppSmokeTests.ProjectStructureProcesses.cs`: `ED674AA153C96C72918416E33EC0B3D4F4B6DADC291354253F68E808921F74B6`

## Implementation Summary
- Project-structure process run and output-folder projection routes now include `processId` and `runId`.
- Routable process pages own `processId`, `runId`, and `launchPlanId` query binding and pass typed GUIDs into `ProcessWorkspace`.
- SB012 integration proof asserts persisted run bridge context, output artifact projection, observation dashboard run readback, and project-structure output-folder route.
- SB012 large-desktop browser proof creates a real project/work node/process/run/output artifact through public APIs, opens the projected output folder quick action, dismisses startup confirmation in the popup, and verifies the exact selected run.

## Build And Test Proof
- `proof/SB012/web-build-no-restore.txt`: `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore` passed with 0 warnings and 0 errors.
- `proof/SB012/project-structure-run-closure-integration.txt`: focused SB012 integration test passed, 1/1.
- `proof/SB012/project-workbench-projection-routes.txt`: focused workbench projection route tests passed, 3/3.
- `proof/SB012/project-structure-run-output-playwright.txt`: focused large-desktop Playwright proof passed, 1/1.
- `proof/SB012/prepared-validator-after-sb012.txt`: prepared-stage bundle validator passed.
- TRX files are under `proof/SB012/test-results`.

## Source And Scan Proof
- `proof/SB012/project-structure-run-closure-source-assertions.txt`
- `proof/SB012/anti-stub-and-runtime-host-drift-scan.txt`
- `proof/SB012/no-transient-bundle-path-scan.txt`
- `proof/SB012/no-unexpected-ui-media-drift-scan.txt`

## Browser Proof
Screenshots are under `proof/SB012/screenshots`:
- `01-structure-run-output-node-large-desktop.png`
- `02-run-output-quick-actions-large-desktop.png`
- `03-run-output-process-workspace-large-desktop.png`

## Semantic Adequacy
See `proof/SB012/semantic-invariants.md`.

## Adversarial Negative Proof
Route-only proof was rejected:
- `proof/SB012/red-team/shallow-route-only-proof.md`
- `proof/SB012/red-team/shallow-route-only-rejection.txt`

## Closure
SB012 is closed. SB013 may proceed.
