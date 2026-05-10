# Web API Navigation And App Integration

## Status

- `Passed with production durable-host follow-up`

## Objective

- Wire workflow APIs, dependency injection, navigation, authorization/validation conventions, and app-level integration so workflow features are usable as part of the web application.
- Ensure workflow API and UI integration align with existing Agents and Processes surfaces without blending their domain responsibilities.
- Decide how CanDoItAll product APIs relate to MAF Azure Functions generated workflow endpoints, RequestPort response/status endpoints, and optional MCP tool exposure.

## Success Criteria

- Workflow endpoints are mapped consistently under the app API route structure.
- Workflow services are registered through existing application composition patterns.
- Durable workflow hosting uses `ConfigureDurableOptions` when agents and workflows are registered together, or documents why workflow-only hosting is correct.
- Generated Azure Functions endpoints are either used through an explicit product boundary, wrapped/proxied by CanDoItAll APIs, or rejected with documented rationale.
- Navigation exposes the Workflows page under the existing Agents module area.
- API/UI integration supports catalog, settings, components, validation, test runs, run control, artifacts, external requests, and process role workflow links.
- Browser and API proof show the integrated app path works.

## Covered Inputs

- RQ-001, RQ-008, RQ-009, RQ-010, RQ-012, RQ-019, RQ-020, RQ-021, RQ-022, RQ-024, RQ-025, RQ-026.
- RN-001, RN-006, RN-007, RN-008, RN-009, RN-010, RN-016, RN-017, RN-018.

## Prerequisites

- Subbundle 03 completed for workflow API/service foundations.
- Subbundle 04 completed for Workflows page.
- Subbundle 05 completed for canvas editor.
- Subbundle 06 completed for process role workflow integration.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ProcessesApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\CanDoItAll.Modules.AgentFramework.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\CanDoItAll.AgentFramework.Hosting.csproj`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.DurableTask\ServiceCollectionExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\Workflows\DurableWorkflowOptionsExtensions.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\Workflows\DurableWorkflowsFunctionMetadataTransformer.cs`
- `C:\repositories\agent-framework\dotnet\src\Microsoft.Agents.AI.Hosting.AzureFunctions\BuiltInFunctions.cs`

## Deliverables

- Workflow API route mapping in the web API route extension or equivalent app routing location.
- Service registration for workflow catalog, settings, components, validation, test runner, runtime manager, persistence, and MAF implementation.
- DurableTask worker/client registration or integration with the selected durable workflow host.
- Decision record for Azure Functions generated endpoints: run endpoint, status endpoint, RequestPort respond endpoint, and optional MCP tool trigger.
- Navigation link or module entry for Workflows page.
- API integration for workflow run control: start/test, status, events, cancel, resume/respond to external request, artifacts.
- App integration for process-to-workflow links and workflow-to-process references where subbundle 06 exposes them.
- Authorization/error/validation behavior aligned with existing Agents/Processes APIs.
- Performance review for workflow API/status/polling/serialization paths.
- Browser proof for navigation and integrated workflow page/canvas/process paths.

## Dependency Impact

- Subbundle 08 depends on this integrated route/API state for final end-to-end validation.
- Future workflow features depend on consistent endpoint names, DI, and navigation.

## Validation Depth

- Web API, durable host integration, DI, navigation, browser-proof, performance-review, and architecture review depth.
- Requires API smoke tests and integrated browser validation.

## Implementation Steps

1. Review existing app startup, API route mapping, and Agents/Processes endpoint conventions.
2. Add workflow service registrations in the approved composition location.
3. Configure DurableTask host integration using `ConfigureDurableOptions` or document the approved exception.
4. Map workflow API endpoints under a clear route such as `/api/workflows` unless existing conventions require another path.
5. Decide whether Azure Functions generated endpoints are used, proxied, disabled, or reserved for a separate host.
6. Wire navigation to the Workflows page in the existing module/app navigation pattern.
7. Ensure API error handling returns explicit validation/runtime errors and does not mask workflow failures.
8. Verify UI calls the mapped endpoints and handles authentication/authorization failures if applicable.
9. Run API smoke tests for list/detail/settings/component/test/run-control/external-request/artifact endpoints.
10. Run targeted performance review for API polling/status/event serialization paths.
11. Run build/tests.
12. Run browser validation for navigation, page load, canvas/test run, and process workflow link.
13. Run architecture review focused on app integration and boundary cleanliness.
14. Update execution report.

## Scope Exceptions

- Do not add new workflow features beyond integrating the completed workflow surfaces.
- Do not revisit phase-1 runtime/library ownership unless validation reveals a blocking architecture issue; if it does, reopen the relevant earlier subbundle.

## Do Not Do

- Do not hide workflow API failures behind generic success responses.
- Do not map workflow endpoints inside `AgentsApi` unless architecture review explicitly accepts that; workflows need their own API surface.
- Do not expose generated Azure Functions endpoints or MCP workflow tools without product authorization, audit, and governance review.
- Do not add duplicate service registrations that create separate runtime manager instances with inconsistent state.
- Do not add navigation that points to a placeholder page.

## Acceptance Checklist

- Workflow APIs are mapped and reachable.
- DI resolves workflow services and MAF implementation.
- DurableTask host registration is proven or explicitly blocked with rationale.
- Azure Functions generated endpoints and MCP exposure have a recorded decision.
- Workflows page is reachable from app navigation.
- API/UI integration covers catalog, settings/components, test run, run status/events, artifacts, external requests, and process references.
- Performance review covers API polling/status/event serialization paths.
- Browser proof shows integrated route flow works.
- Architecture review accepts API/navigation boundary.

## Proof Required

- Passed: `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx --no-restore --verbosity minimal -m:1 -p:OutDir=C:\repositories\CanDoItAll\.codex\tmp\workflow-sln-build-9\` with 0 warnings/errors.
- Passed: `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter FullyQualifiedName~WorkflowApiIntegrationTests --verbosity minimal -m:1 -p:OutDir=C:\repositories\CanDoItAll\.codex\tmp\workflow-api-regression-07\` with 4/4 tests passing.
- Passed: `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter FullyQualifiedName~ProcessWorkflowExecutorIntegrationTests --verbosity minimal -m:1 -p:OutDir=C:\repositories\CanDoItAll\.codex\tmp\process-workflow-tests-10\` with 5/5 tests passing, including process run detail API workflow links and scoped assignment-resolution workflow id mapping.
- Passed: workflow unit tests `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~Workflow --verbosity normal -m:1 -p:OutDir=C:\repositories\CanDoItAll\.codex\tmp\workflow-unit-tests-9\ -p:IntermediateOutputPath=C:\repositories\CanDoItAll\.codex\tmp\workflow-unit-tests-obj-9\` with 16/16 tests passing.
- Passed: workflow component tests `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter FullyQualifiedName~WorkflowsPageTests --verbosity normal -m:1 -p:OutDir=C:\repositories\CanDoItAll\.codex\tmp\workflow-component-tests-7\ -p:IntermediateOutputPath=C:\repositories\CanDoItAll\.codex\tmp\workflow-component-tests-obj-7\` with 3/3 tests passing.
- DurableTask host registration: documented blocker. The repo references `Microsoft.Agents.AI.Workflows` and MAF runtime packages, but no project currently references `Microsoft.Agents.AI.DurableTask` or `Microsoft.Agents.AI.Hosting.AzureFunctions`; the app therefore cannot honestly register `ConfigureDurableOptions` in this pass without adding a new package/deployment decision. Production durable execution remains a follow-up gate, and the in-process backend remains limited to preview/test/short non-durable execution.
- Azure Functions generated endpoints/MCP decision: rejected for the current web app host until product authorization/audit and DurableTask package/host selection are approved. CanDoItAll product APIs under `/api/workflows` remain the exposed surface for catalog, validation, run status/events, artifacts, and external requests.
- Performance review: hot-path scan found no real blocking waits, no `Task.Run`, no `Thread.Sleep`, no culture-sensitive `ToLower`/`ToUpper`, no runtime regex, and no string comparison issues requiring code changes. LINQ hits are EF/query or UI/API projection paths; in-memory ordering is deliberate where SQLite `DateTimeOffset` translation is unsafe.
- Browser proof: navigation/page/canvas proof remains from subbundles 04/05, and process workflow-link proof is recorded in subbundle 06 screenshots.

## Browser Validation Logging

- Route: `/agents`, `/agents/workflows`, `/api/workflows/*`, and process workflow link surfaces under `/processes`.
- Viewports: desktop and narrower-width screenshots from subbundles 04, 05, and 06.
- Playwright evidence: Agents shell exposes `Open workflows`; `/agents/workflows` loads catalog/canvas/test-run state; process role editor selects workflow executor; process run execution tab shows the linked workflow run ledger.
- Screenshots:
  - `C:\repositories\CanDoItAll\.codex\bundles\ai-workflows-maf-integration\reviews\evidence\subbundle-04\workflow-desktop-proof.png`
  - `C:\repositories\CanDoItAll\.codex\bundles\ai-workflows-maf-integration\reviews\evidence\subbundle-05\workflow-canvas-desktop-proof.png`
  - `C:\repositories\CanDoItAll\.codex\bundles\ai-workflows-maf-integration\artifacts\browser\subbundle-06-workflow-run-ledger-desktop.png`
- Review result: integrated routes expose real workflow data and process workflow links; no placeholder-only workflow screen remains.

## Progression Gate

- Final closure may proceed only after workflow APIs, durable host registration decision, DI, navigation, performance review, and integrated browser flow are proven.

## Suggested Agent Prompt

```text
Implement subbundle 07 only.
Wire workflow APIs, service registration, navigation, and app integration for the surfaces built in earlier subbundles.
Do not introduce new feature scope beyond integration.
Capture API and browser proof and update reviews/01-execution-report.md.
```
