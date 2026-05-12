# Workflow API Gap Closure

## Status

- `Completed`

## Objective

- Add the smallest justified workflow API commands missing from the current development control surface.

## Covered Inputs

- N001
- R001
- R002

## Prerequisites

- Prepared-stage bundle validator has passed.
- Current API review confirms lifecycle/import/export are still missing.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\PersistentWorkflowStores.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowCatalogModels.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkflowApiIntegrationTests.cs`

## Deliverables

- Lifecycle endpoints and typed request DTO.
- Import/export endpoints and typed envelope DTO.
- Service methods that preserve workflow graph/runtime policy and centralize save/version behavior.
- Integration tests for new commands.

## Dependency Impact

- Subbundle 02 documents the final workflow API route list. Weak or incomplete API proof would make the skill inaccurate.

## Validation Depth

- Critical API foundation.

## Implementation Steps

1. Add workflow catalog service methods for status change and import/export.
2. Add workflow catalog models for lifecycle request and export/import envelope.
3. Add endpoints to `WorkflowsApi`.
4. Add targeted integration tests.
5. Run targeted workflow API tests.

## Scope Exceptions

- Do not add process step, escalation, direct-message, or assignment endpoints to workflows unless the workflow domain has matching objects.
- Do not implement production DurableTask/Azure hosting.

## Do Not Do

- Do not reintroduce workflow MCPs.
- Do not use generic string command endpoints.
- Do not expose persistence entity records directly.

## Acceptance Checklist

- Lifecycle routes return updated workflow definitions.
- Publish validates the definition before activation.
- Export returns a typed envelope for the current definition.
- Import saves a new definition through the catalog service.
- OpenAPI exposes the new routes.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter WorkflowApiIntegrationTests`
- If targeted tests cannot run, record the exact blocker and run `dotnet build CanDoItAll.slnx --no-restore`.

## Browser Validation Logging

- N/A - direct API implementation and integration tests only.

## Progression Gate

- Pass only when workflow lifecycle/import/export route behavior is covered by targeted tests or a precise validation blocker is recorded.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Add typed workflow lifecycle and import/export API commands with targeted tests. Preserve graph/runtime policy when changing lifecycle status, validate publish before activation, and stop if the workflow domain exposes a larger missing command surface than this bundle captured.
```
