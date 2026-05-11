# 10 PostgreSQL Test DB, Projects, and Real-World Scenarios

## Status

- `Completed`

## Objective

Create a new PostgreSQL test database, run the testing instance against it, seed projects/project structures and 20 real-world workflow examples, and repair discovered executor/project-structure issues.

## Covered Inputs

- `inputs/03-follow-up-request.md`: create a new PostgreSQL database for this test, seed 20 real-world workflow examples, add projects with project structures, test file operations, asset-node creation, and repair broken behavior.

## Prerequisites

- Subbundle `08` passed for authoring UX.
- Subbundle `09` passed for observer APIs.
- PostgreSQL is reachable on the developer machine or a blocker is recorded with exact command output.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\SwitchableAppDbContextFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseProfileControlPlaneService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProjectsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\BuiltInWorkflowExecutors.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkflowExecutorTests.cs`

## Deliverables

- New PostgreSQL database dedicated to this test run.
- Documented connection/profile used by the testing instance with secrets masked in reports.
- Seeded projects with project structures and assets sufficient for project-structure read/write executor scenarios.
- 20 real-world workflow examples seeded into the running test instance through API or durable persistence.
- Scenario execution report covering success and non-happy paths across storage, HTTP, spreadsheet, project structure, image/provider, retry/timeout, and observer controls.

## Dependency Impact

- This subbundle is the end-to-end proof gate.
- It can reopen executor/runtime/API/UI work if real scenarios expose defects.

## Validation Depth

- Database creation proof.
- Migration/startup proof.
- API seeding proof.
- Scenario execution proof.
- Provider smoke attempts.

## Implementation Steps

1. Resolve local PostgreSQL connection strategy without exposing credentials in bundle reports.
2. Create a unique test database.
3. Run migrations/start the app against that database.
4. Seed projects and project structures.
5. Seed 20 workflows/examples through APIs or a deterministic test seeder.
6. Execute the scenario matrix and repair failures that are in current scope.
7. Record any exact blocker for unavailable provider/model/image bridges.

## Scope Exceptions

- If workflow definitions remain in-memory, document that workflow examples were seeded into the running test instance via API, not persisted in PostgreSQL.
- Exact `gptoss20b64k` may be blocked if the Ollama model name is not installed or cannot be pulled; nearest installed fallback may be recorded but does not fully solve the exact model request.

## Do Not Do

- Do not mutate or reset the user’s existing app database.
- Do not log raw connection strings or secrets.
- Do not call a scenario “seeded into PostgreSQL” if it only exists in an in-memory singleton.

## Acceptance Checklist

- New PostgreSQL DB exists and the app can run against it.
- At least 20 examples are created in the testing instance.
- Projects/project structures exist for complex executor tests.
- Scenario report identifies passed, repaired, partial, and blocked cases honestly.

## Proof Required

- PostgreSQL database creation command result.
- App startup route/API proof against the test database.
- Scenario seed count and workflow definition count.
- Scenario execution results and provider attempts.

## Browser Validation Logging

- Record any browser proof for seeded workflow/project views in `reviews/01-execution-report.md`.

## Progression Gate

- Pass only when 20 examples have been seeded and enough real scenarios have executed to validate the repaired canvas/API/runtime path.

## Suggested Agent Prompt

Implement subbundle 10 only. Create a new PostgreSQL test database, run the app against it, seed project structures and workflow examples, execute the scenario matrix, and repair scoped defects.
