# Workflow Catalog Settings API And Tests

## Status

- `Ready`

## Objective

- Add the service/API foundation for workflow definitions, workflow settings, prepared LLM Call Components, workflow validation, and workflow test runs.
- Keep these capabilities workflow-specific while matching the operational clarity users already have in process APIs and views.

## Success Criteria

- Workflow definitions can be created, read, updated/versioned, validated, and listed through application services and APIs.
- Workflow settings and LLM Call Components have strongly typed contracts and validation.
- Workflow test runs can execute through the runtime manager and return structured results, events, artifacts, and failures.
- API tests cover success, validation failure, and runtime failure paths.

## Covered Inputs

- RQ-001, RQ-008, RQ-009, RQ-011, RQ-014, RQ-015, RQ-016, RQ-020, RQ-021, RQ-026.
- RN-001, RN-007, RN-009, RN-012, RN-013, RN-018.

## Prerequisites

- Subbundle 01 completed and architecture review passed.
- Subbundle 02 completed for runtime-backed test execution.
- Workflow definition/component/run contracts exist.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\CanDoItAll.AgentFramework.Models.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\ProviderModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\MafAgentRuntime.AgentFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\CanDoItAll.AgentFramework.Persistence.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesService.Reads.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs`

## Deliverables

- Workflow catalog service for definition list/detail/version lifecycle.
- Workflow settings service and validation rules for provider/model defaults, runtime options, artifact policy, and human-in-loop policy.
- LLM Call Component library service with create/list/detail/update/version or equivalent lifecycle.
- Workflow validation service that validates graph connectivity, node kinds, component references, input/output shape compatibility, provider capability compatibility, and unsupported MAF mappings.
- Workflow test runner API/service that executes a draft or saved workflow against sample input and returns structured result, event trace, artifacts, validation issues, and runtime errors.
- API endpoints and tests for workflow catalog, settings, components, validation, and test runs.
- API/runtime performance review for validation and test-run paths, especially graph validation, event/result serialization, and status polling.

## Dependency Impact

- Subbundle 04 depends on catalog/settings/test APIs for the Workflows page.
- Subbundle 05 depends on component library and graph validation APIs.
- Subbundle 06 depends on discoverable, runnable workflow definitions for process role assignment.
- Subbundle 07 depends on route registration and endpoint conventions established here.

## Validation Depth

- API, service, model-validation, runtime-test, and performance-review depth.
- Requires architecture review focused on API contract boundaries and workflow/process separation.

## Implementation Steps

1. Review process services/API only for interaction patterns, not for model reuse.
2. Implement workflow catalog/settings/component services using models from subbundle 01.
3. Implement validation for workflow graph and LLM Call Component compatibility.
4. Implement test-run orchestration through the workflow runtime manager from subbundle 02.
5. Add API endpoints or endpoint groups according to existing web API conventions.
6. Add tests for CRUD/list/detail validation, component validation, test-run success, test-run validation failure, and test-run runtime failure.
7. Run targeted performance scan/review for validation/test-run/status endpoints and avoid sync-over-async or allocation-heavy polling loops.
8. Verify no raw MAF types appear in API request/response contracts.
9. Run build/tests.
10. Run architecture review for API/settings/component boundaries.
11. Update execution report.

## Scope Exceptions

- UI implementation is limited to API/service support if absolutely needed for test harness plumbing; full page/canvas UI belongs to subbundles 04 and 05.
- Process role assignment belongs to subbundle 06.

## Do Not Do

- Do not make workflow settings a copy of process settings.
- Do not store LLM component provider/model choices as arbitrary strings if typed provider/model references exist.
- Do not allow test-run endpoints to mutate published workflow versions accidentally.
- Do not hide provider/model capability mismatches by choosing another provider/model.

## Acceptance Checklist

- Workflow catalog/settings/components/test-run APIs exist and use typed contracts.
- Validation catches disconnected graphs, missing component references, invalid provider/model settings, unsupported modality, and incompatible result shape.
- Test runner returns structured events/artifacts/errors.
- API tests cover positive and negative cases.
- Performance review notes exist for validation, serialization, and status/test-run API paths.
- Architecture review confirms API contracts do not leak raw MAF types.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- API/service test command covering workflow catalog, settings, components, validation, and test runs.
- Performance scan/review excerpt for workflow API hot paths.
- Execution report entries for API contract review and architecture review.

## Browser Validation Logging

- API-first subbundle. Browser validation is N/A unless implementation adds a visible UI test harness.
- If UI is touched, record route, maximized desktop viewport, narrower-width viewport, Playwright actions, assertions, screenshots, and screenshot review notes.

## Progression Gate

- Workflows page and canvas work may proceed only after catalog/settings/component/test APIs are stable enough for UI consumption and tests pass.

## Suggested Agent Prompt

```text
Implement subbundle 03 only.
Build workflow catalog, settings, component library, validation, and test-run API/service foundation.
Do not implement the full workflow page, canvas editor, or process role integration.
Keep API contracts typed and free of raw MAF runtime types.
```
