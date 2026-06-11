# SB02 Semantic Invariants

## Invariant SB02_INV_001
- Invariant ID: `SB02_INV_001`
- Source raw note: Determine whether process execution works again like before from the user/operator perspective.
- Expected behavior: A 1900x1200 browser can start from a project/project-structure context, import the representative `Business plan development` template, link the published process definition to a project-structure work item, start it through the visible project-structure process action, review HR/AI assignments, approve/execute the launch plan through the UI path, navigate to `route:projects-{projectId}-processes-query-processId-{definitionId}-runId-{runId}`, and read back the durable run with project id, generated run id, selected-run summary, and step-run details.
- Disallowed shallow implementation: API-only run creation, screenshot-only proof without API readback, launching outside project context, bypassing launch-plan approval/execution, or proving only a static report without the browser route and durable run id.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt`
- Passing test: `bundle://proof/SB02/transcripts/focused-playwright.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`; `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectStructureProcesses.cs`
- Production assertions: The Playwright proof asserts `ProcessLaunchPlanStatus.Executing`, non-null generated run id, project-scoped run readback, project-structure context JSON in the launch trigger, selected-run summary text, durable step-run count, and no visible Blazor error UI.
- Red-team negative case: `bundle://proof/SB02/transcripts/failing-first-source-assertion.txt` proves the baseline branch lacks the SB02 invariant test, assignment action constants, outline-node action path, and project-structure context assertion.
- Downstream dependency check: SB03-SB04 still own deterministic process-mock runtime dispatch; SB06 can reuse the project-scoped run detail surface proven by `SB02_INV_001`.


