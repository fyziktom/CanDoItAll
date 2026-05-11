# 09 Workflow Control APIs and Observer Contract

## Status

- `Completed`

## Objective

Bring workflow control APIs closer to the process API control surface so scenario tests can act as a human observer without relying only on UI clicks.

## Covered Inputs

- `inputs/03-follow-up-request.md`: assure up-to-date APIs for controlling workflows similar as processes during tests where Codex plays a human observer.

## Prerequisites

- Subbundle `04` runtime manager and run store behavior remains valid.
- Subbundle `08` can depend on these APIs only after this subbundle closes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowRuntimeManager.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs`

## Deliverables

- API endpoint for executor catalog descriptors.
- API endpoint for runtime backend descriptors.
- Explicit workflow run start endpoint for saved definitions.
- Explicit workflow run cancel endpoint.
- Workflow analytics or dashboard summary endpoint.
- Template/example endpoint for seeded workflow examples if the implementation adds templates.
- API tests or endpoint smoke script that proves observer operations can list, start, inspect, cancel/respond, and collect artifacts/events.

## Dependency Impact

- This is a critical testing foundation for the PostgreSQL scenario subbundle.
- Without these endpoints, scenario proof becomes UI-only and much weaker.

## Validation Depth

- Focused API tests or real HTTP smoke against the local app.
- Full build validation.

## Implementation Steps

1. Compare `WorkflowsApi` to `ProcessesApi` and add the smallest missing observer endpoints.
2. Keep endpoint names explicit and typed.
3. Reuse existing services before adding new abstractions.
4. Add DTOs only where direct domain models would expose the wrong shape.

## Scope Exceptions

- Durable Task Scheduler APIs and Azure Functions generated HTTP endpoints remain outside this subbundle unless already registered in the app.
- Full workflow persistence is only implemented if needed to satisfy seeded PostgreSQL proof.

## Do Not Do

- Do not create stringly typed run commands.
- Do not add silent fallback behavior for missing backends or executors.

## Acceptance Checklist

- Workflow APIs expose executor catalog and backend catalog.
- Saved workflow runs can be started and cancelled through HTTP.
- Runs, events, artifacts, pending requests, and analytics can be observed through HTTP.
- API failures return predictable problem results.

## Proof Required

- `dotnet build CanDoItAll.slnx --no-restore`
- HTTP smoke or tests against `/api/workflows/...` endpoints.

## Browser Validation Logging

- N/A for direct API implementation.
- Browser proof is covered by subbundle 08 and 11.

## Progression Gate

- Pass only when the 20-scenario test harness can use APIs for observer-style control.

## Suggested Agent Prompt

Implement subbundle 09 only. Compare workflow endpoints to process endpoints and add the smallest typed workflow control surface needed for scenario tests.
