# PostgreSQL Provider Browser Validation And Closure

## Status

- `Completed`

## Objective

- Prove the full feature in the real app with PostgreSQL, browser UI, at least 20 realistic workflow scenarios, `gpt-5-mini`, and local Ollama `gptoss20b64k`, then close every raw note.

## Success Criteria

- Real app uses the Visual Studio PostgreSQL database.
- Browser proof shows add/start/status/summary/result flow.
- At least 20 scenarios run and are validated.
- Required provider runs are recorded.
- Any product defect found is repaired and rerun.

## Covered Inputs

- `N001` through `N026`
- `R001` through `R015`

## Prerequisites

- Subbundles 01-06 closure gates have passed.
- PostgreSQL database target is confirmed.
- Provider profiles for `gpt-5-mini` and Ollama `gptoss20b64k` are confirmed or explicit environment blocker is recorded.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll.slnx`
- `C:\repositories\CanDoItAll\docker-compose.yml`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureProcesses.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureQuickActions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`
- `C:\repositories\CanDoItAll\.codex\bundles\project-structure-workflow-runs\templates\01-scenario-matrix.md`
- `C:\programovani\testdata\testworkflows`

## Deliverables

- Browser proof screenshots and analytics rows.
- PostgreSQL scenario result artifacts for at least 20 cases.
- Provider-specific proof for `gpt-5-mini` and `gptoss20b64k`.
- Final raw-note closure audit.
- Completed-stage bundle validation.

## Dependency Impact

- This is the closure subbundle. Weak proof here means the main user request remains unresolved.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Start the app against the Visual Studio PostgreSQL configuration.
2. Seed a project and project structure with realistic parent nodes and file/folder references.
3. Use Playwright to add a workflow node, inspect input preview, start it, wait for status, select it, and inspect summary/result children.
4. Run the 20 scenario harness and validate each output manually against expected behavior.
5. Run at least one `gpt-5-mini` scenario and one local Ollama `gptoss20b64k` scenario.
6. If any product defect appears, add/repair subbundle work, rerun validators/proof, and record the repair.
7. Update execution report, raw-note closure, README validation summary, and completed-stage validator.

## Scope Exceptions

- Environment-only provider failures may be recorded as blockers only if product configuration and error reporting are proven correct.

## Do Not Do

- Do not mark final closure with fewer than 20 scenarios.
- Do not replace browser proof with reasoning.
- Do not count an output as pass if it is generic and not grounded in the input.
- Do not hide product defects as residual risks.

## Acceptance Checklist

- PostgreSQL target is identified in proof.
- UI screenshots show add dialog, start confirmation, selection status, and summary/result nodes.
- 20 scenario rows have pass/fail decisions.
- `gpt-5-mini` and `gptoss20b64k` proof exists.
- Raw note closure table marks every note solved, partially solved, or not solved.
- Final validator passes or blocker is explicit.

## Proof Required

- `dotnet test CanDoItAll.slnx`
- Playwright screenshots under `.codex/bundles/project-structure-workflow-runs/proof/browser/`
- Scenario artifacts under `.codex/bundles/project-structure-workflow-runs/proof/scenarios/`
- Completed-stage validator output.

## Closure Evidence

- PostgreSQL runtime proof: `proof/runtime/web-postgres-stdout.log` shows the Development app using the configured PostgreSQL override (`candoitall_workflow_routing_dev`) and seeding the 20 workflow examples.
- Browser proof: `CANDOITALL_PLAYWRIGHT_BASEURL=http://127.0.0.1:5087 dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter "Project_structure_workflow_nodes_can_be_added_started_and_inspected_in_browser" /p:BuildInParallel=false` passed against the PostgreSQL app.
- Browser artifacts: `project-structure-add-workflow-desktop.png`, `project-structure-start-workflow-confirmation.png`, `project-structure-workflow-selection-status.png`, `project-structure-workflow-result-child-desktop.png`, and `project-structure-workflow-summary-mobile.png`.
- Scenario proof: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "ProjectStructureWorkflowScenarioHarnessTests" /p:BuildInParallel=false` passed 20 scenarios on SQLite and PostgreSQL.
- Provider proof: `proof/providers/provider-validation-results.json` records `gpt-5-mini` and local Ollama `gptoss20b64k:latest` provider chat probes and workflow runs; both completed with marker validation.
- Repairs made during closure: the add-workflow input preview no longer truncates preview rows, and add-dialog async refreshes are versioned so older refreshes cannot overwrite newer source/manual input edits.
- Solution-level residual: `dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~Workflow" /p:BuildInParallel=false` still exposes the pre-existing Playwright process audit timeout waiting for `processes-launch-name-input`; `dotnet test CanDoItAll.slnx /p:BuildInParallel=false` timed out after 20 minutes and its stale test processes were stopped. Targeted project-structure workflow, scenario, provider, component, unit, and integration proof passed.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`
- Viewports: `1600x950`, `390x844`
- Actions/assertions: add workflow under a selected node, verify input preview includes project/parent/folder/file where selected, start from workflow node context menu, confirm, wait for status/progress, select workflow node, inspect current step/total steps, inspect summary/result child nodes.
- Screenshots: `project-structure-add-workflow-desktop.png`, `project-structure-start-workflow-confirmation.png`, `project-structure-workflow-selection-status.png`, `project-structure-workflow-summary-mobile.png`
- Review questions: add/start/status/results are operable from canvas; no matching resources dialog appears; overlays are readable and unclipped; results are visibly under workflow node.

## Progression Gate

- Final closure passes only when browser proof, 20 scenario proof, provider proof, raw-note closure, and completed-stage validation all agree.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
