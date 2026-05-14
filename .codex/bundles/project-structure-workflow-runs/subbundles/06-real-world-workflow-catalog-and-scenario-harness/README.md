# Real-World Workflow Catalog And Scenario Harness

## Status

- `Completed`

## Objective

- Add or improve workflow definitions and a repeatable scenario harness for at least 20 realistic cases, including the supplied Mouser, SEAMARK, and financial-plan data.

## Success Criteria

- At least 20 distinct scenario definitions exist.
- Workflows contain real-world instructions and expected validations.
- Harness can seed project-structure data and run scenarios against PostgreSQL.
- Mouser, SEAMARK, and financial workbook scenarios are included.

## Covered Inputs

- `N021`, `N022`, `N023`, `N024`
- `R012`, `R013`

## Prerequisites

- Subbundle 05 closure gate has passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessWorkflowExecutorIntegrationTests.cs`
- `C:\programovani\testdata\testworkflows\mouser-order\Cart_Mar30_1059AM.xls`
- `C:\programovani\testdata\testworkflows\mouser-order\MOUSER_Receipt_89566550.pdf`
- `C:\programovani\testdata\testworkflows\IoTFactory rozpočet-v1.xlsx`
- `C:\programovani\testdata\testworkflows\SEAMARK`

## Deliverables

- Workflow examples or seed data for Mouser, SEAMARK, financial plan, and synthetic business cases.
- Scenario harness producing structured result artifacts under the bundle proof folder.
- Scenario matrix expanded as needed during implementation.
- Validations that check outputs are grounded and meaningful.

## Dependency Impact

- Subbundle 07 closure depends on this harness. If scenarios are trivial or duplicate, the final validation cannot satisfy the user's 20-case requirement.

## Validation Depth

- `End-to-end scenario foundation`

## Implementation Steps

1. Inventory existing workflow examples and keep useful generic workflows.
2. Add workflow definitions for Mouser reconciliation/summary, SEAMARK folder summary/product comparison/price extraction, and financial plan review.
3. Add synthetic scenario inputs for emails, business plans, support, release, meeting, vendor, incident, and file-save cases.
4. Add a harness that seeds a PostgreSQL project and project-structure nodes/files.
5. Add validation checks for expected grounded results.
6. Write scenario result artifacts to the bundle proof folder.
7. Update execution report scenario rows.

## Scope Exceptions

- Provider-specific final runs are owned by subbundle 07, but this harness must make them possible.

## Do Not Do

- Do not count duplicate prompts with different names as separate real-world cases.
- Do not accept generic summaries that ignore supplied files.
- Do not mutate unrelated workflow examples unless needed for the scenario matrix.

## Acceptance Checklist

- Scenario matrix has at least 20 cases.
- Mouser scenarios read both XLS and PDF paths.
- SEAMARK scenarios accept the folder as input and expose it in preview/summary.
- Financial scenario reads the provided workbook.
- Synthetic cases have realistic instructions and expected checks.

## Proof Required

- Scenario harness command and output artifact path.
- `dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~Workflow"`
- Scenario result file under `.codex/bundles/project-structure-workflow-runs/proof/scenarios/`.

## Browser Validation Logging

- N/A. Browser validation is owned by subbundle 07.

## Progression Gate

- `Passed`: harness ran 20 defined scenarios against SQLite and PostgreSQL API hosts and produced validation artifacts.

## Implementation Notes

- Added five managed workflow examples for Mouser reconciliation, Mouser purchasing summary, SEAMARK folder summary, SEAMARK pricing extraction, and IoTFactory financial plan review.
- Updated seeded LLM instructions so project-structure workflow runs preserve `projectId` from `project.id` and `nodeId` from `runContext.workflowNodeId`, allowing result assets to be stored under workflow nodes.
- Added `source.ingest` to the seeded project-structure summary workflows so file/folder sources are loaded into bounded PDF/XLS/XLSX/text content before the LLM step.
- Tightened SEAMARK instructions with explicit X-5600/ZM-x5600, X-6600/ZM-x6600, and X-6600A/ZM-x6600A price mappings after provider proof caught a swapped-price result.
- Added a reusable project-structure scenario harness integration test that seeds parent, child, selected-node, file, folder, and manual JSON inputs through the project-structure API.
- Covered 20 distinct scenarios, including supplied Mouser XLS/PDF files, SEAMARK PDFs, the IoTFactory workbook, synthetic emails, business plan, support, release, incident, folder intake, file-save, subtree, prompt cleanup, and compliance cases.
- Added a PostgreSQL variant using the existing test PostgreSQL availability helper and a temporary database.

## Closure Evidence

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "ProjectStructureWorkflowScenarioHarnessTests" /p:BuildInParallel=false --no-restore` passed: 2 tests, including SQLite and PostgreSQL 20-scenario harness runs.
- Scenario artifacts written:
  - `.codex/bundles/project-structure-workflow-runs/proof/scenarios/scenario-harness-results.json`
  - `.codex/bundles/project-structure-workflow-runs/proof/scenarios/scenario-harness-postgresql-results.json`
- `dotnet test CanDoItAll.slnx --filter "FullyQualifiedName~Workflow" /p:BuildInParallel=false` built and passed unit, component, and integration workflow-filter suites, including 26 integration tests; command still failed because an existing Playwright process audit test timed out waiting for `processes-launch-name-input`.
- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py --stage prepared .codex\bundles\project-structure-workflow-runs` passed after subbundle 06 bundle updates.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
