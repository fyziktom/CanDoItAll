# SB02 Proof Manifest

## Scope
- Subbundle: `SB02`
- Invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Test name: `Project_structure_process_template_launch_SB02_INV_001_launches_approved_template_from_structure_context_and_reads_back_run`
- Source files:
  - `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`
  - `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`

## Changed-File Hashes
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` SHA-256: `15A9B0AA071373BBB1871F9E6B1D1338D11183CE838D9235496E13B21E6C6126`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs` SHA-256: `EFF2DAB1C91EDCEC754F768253971D7DFEBECC2D59F5DFA718DB95CFED99EE97`

## Source Proof
- Added a large-desktop Playwright flow in `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs` that creates a project-structure work item, imports the `Business plan development` template through the UI, publishes and links the definition, starts the process through the project-structure outline action, reviews HR/AI assignments, executes the launch plan, and asserts run-detail readback.
- Reused the existing project-structure agent API client by adding optional agent identity parameters in `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`.
- Source assertion transcript: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Test Proof
- Passing transcript: `bundle://proof/SB02/transcripts/focused-playwright.txt`
- Command: `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --configuration Debug --no-restore --filter FullyQualifiedName~Project_structure_process_template_launch_SB02_INV_001_launches_approved_template_from_structure_context_and_reads_back_run`
- Result: focused Playwright test passed with exit code 0.

## Adversarial Negative Proof
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt`
- The failing-first source assertion exits non-zero against `HEAD` because the baseline did not contain `SB02_INV_001`, the assignment review-start contract, the project-structure outline action helper, or the project-structure context trigger assertion.

## Browser Evidence
- Viewport: `1900x1200`
- Route path exercised: `route:projects-{projectId}-processes`, `route:projects-{projectId}-structure`, `route:projects-{projectId}-processes-query-processId-{definitionId}-runId-{runId}`
- Screenshot artifacts:
  - `bundle://proof/SB02/screenshots/01-project-template-selected-large-desktop.png`
  - `bundle://proof/SB02/screenshots/02-project-template-linked-structure-large-desktop.png`
  - `bundle://proof/SB02/screenshots/03-project-structure-start-confirm-large-desktop.png`
  - `bundle://proof/SB02/screenshots/04-project-structure-assignment-review-large-desktop.png`
  - `bundle://proof/SB02/screenshots/05-project-structure-assignment-ready-large-desktop.png`
  - `bundle://proof/SB02/screenshots/06-project-run-detail-large-desktop.png`
  - `bundle://proof/SB02/screenshots/07-project-run-steps-large-desktop.png`

## Semantic Adequacy
- Raw note owned: Determine whether processes work like before from a user-visible project/project-structure launch flow.
- Shipped behavior: `SB02_INV_001` proves the normal browser entry path creates a launch plan with serialized project-structure context, uses the live assignment dialog, executes into a durable project-scoped run, redirects to run detail, and reads back selected run and step-run state.
- Shallow-pass trap: An API-only start or a static screenshot could create a run without proving the project-structure launch entry point, HR/AI assignment dialog, launch-plan execution, route redirect, and durable run-detail readback.
- Process-mock boundary: SB02 proves the user-visible launch/readback route with live HR/AI catalog assignments. Deterministic process-mock runtime dispatch remains owned by SB03/SB04.



