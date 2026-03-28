# Central Project-Structure Agent API, Locking, Checklist, Import, And Analytics Foundation

## Status

- `Completed`

## Objective

- Add the central domain and HTTP foundation that makes remote project-structure MCP access possible without duplicating persistence or bypassing the main CanDoItAll machine.

## Covered Inputs

- `R001`, `R002`, `R003`, `R004`
- `R007`, `R008`
- `R009`, `R010`
- `R013`, `R015`, `R016`
- `N001`, `N002`, `N003`, `N005`, `N006`, `N007`, `N008`, `N009`, `N013`

## Prerequisites

- `- none`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\ProjectStructureAgentApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAgentService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureLeaseService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureChecklistService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureImportService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAnalyticsService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectStructureAgentApiIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectStructureAgentIntegrationTests.cs`

## Deliverables

- Central web API contracts and route mapping for project-structure MCP access.
- Central lease persistence and enforcement for project or repo-branch scopes.
- Checklist query service with prerequisite and effective-priority propagation.
- Asset revision flow that creates a new child asset node under an original asset node.
- Import orchestration seam with first-pass supported formats and explicit warnings.
- Operation analytics persistence for project-structure MCP traffic.
- Automated tests that prove the foundation before the client subbundle starts.

## Dependency Impact

- `02` depends on the central policy integration points and analytics persistence created here.
- `03` depends on stable API contracts, central auth hooks, and conflict semantics.
- `04` depends on trustworthy checklist behavior, asset revision rules, and lease conflicts; weak proof here invalidates end-to-end results.

## Validation Depth

- `Critical foundation`
- `Service, API, and integration proof before downstream work`

## Implementation Steps

1. Add typed central API request and response models for projects, structure, checklists, leases, assets, imports, and analytics.
2. Add lease storage and conflict-reporting services with project and repo-branch scope support.
3. Add checklist derivation and asset revision behavior on top of existing workbench and project services.
4. Add import orchestration that routes supported descriptions into existing domain services and records warnings.
5. Map the new API endpoints into the web app startup flow.
6. Add automated tests for central behavior before starting the remote MCP client.

## Scope Exceptions

- If a format example from the raw request cannot be fully implemented in this phase, record the exact unsupported construct and keep the import seam explicit instead of hiding it.

## Do Not Do

- Do not add direct remote DB access from MCP machines.
- Do not create a second project-structure domain model.
- Do not enforce leases only in the MCP client.
- Do not silently overwrite existing asset nodes.

## Acceptance Checklist

- Central API exposes typed routes for the planned MCP operations.
- Lease conflicts are centrally enforced and return actionable owner details.
- Checklist results include unfinished items, prerequisites, and effective priority.
- Asset revision flow creates a new asset node beneath the original asset node.
- Import requests use explicit supported-format handling and warnings.
- Analytics capture enough context to support later audit.
- Automated tests cover the foundation sufficiently for downstream work.

## Proof Required

- `dotnet test` for updated or new service tests
- `dotnet test` for API or integration tests against the central web app
- A recorded example of lease-conflict output
- A recorded example of checklist priority propagation
- A recorded example of asset revision behavior

## Browser Validation Logging

- `- N/A for this subbundle because the main shipped behavior is central service and API foundation, not a browser-visible change.`
- `- Any accidental UI coupling discovered here is a reopen signal, not a substitute for subbundle 02 browser proof.`

## Progression Gate

- Automated proof shows central API contracts, checklist behavior, asset revision behavior, and lease conflicts are stable enough that policy UI and MCP client work can proceed without guessing.

## Suggested Agent Prompt

```text
Implement the central project-structure MCP foundation only. Reuse ProjectsService and ProjectWorkbenchService, add the smallest central API and shared services needed, and prove leases, checklist logic, asset revisioning, and analytics with automated tests before moving on.
```
