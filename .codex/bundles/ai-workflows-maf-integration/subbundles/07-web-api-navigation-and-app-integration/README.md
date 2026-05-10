# Web API Navigation And App Integration

## Status

- `Ready`

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

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- API smoke/integration test command for workflow endpoints.
- DurableTask host registration proof or documented blocker.
- Performance scan/review excerpt for workflow API hot paths.
- Browser screenshots for navigation to Workflows page, canvas/test run, and process workflow link if present.
- Execution report route, viewport, Playwright evidence, screenshots, and architecture review notes.

## Browser Validation Logging

- Route: app navigation to Workflows page and related process workflow links.
- Viewports: maximized desktop and narrower-width.
- Playwright evidence: open app, navigate to Workflows, load catalog, open canvas/test run, inspect run status/artifacts, follow process workflow link if available.
- Screenshots: navigation entry, Workflows page integrated state, canvas/test result, process link state.
- Review questions: verify no broken links, no placeholder-only screens, errors are actionable, and UI remains within existing app patterns.

## Progression Gate

- Final closure may proceed only after workflow APIs, durable host registration decision, DI, navigation, performance review, and integrated browser flow are proven.

## Suggested Agent Prompt

```text
Implement subbundle 07 only.
Wire workflow APIs, service registration, navigation, and app integration for the surfaces built in earlier subbundles.
Do not introduce new feature scope beyond integration.
Capture API and browser proof and update reviews/01-execution-report.md.
```
