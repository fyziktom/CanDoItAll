# HTTP API Contract Repairs

## Status

- `Completed`

## Objective

- Repair focused API contract and route exposure coverage so tests and OpenAPI assertions match current source routes.

## Success Criteria

- High-risk Cognitive Memory routes and v1 aliases are asserted in focused API/OpenAPI tests.
- Any source route missing from API contract exposure is fixed or explicitly documented as intentionally hidden.
- Docs/skills phases have reliable API contract proof.

## Covered Inputs

- RQ-002 API contract and route exposure repairs.
- GAP-003 focused OpenAPI coverage omissions.
- Cognitive Memory `/contract`, `/projections/rebuild`, `/automation/run`, `/retention/cleanup`, and v1 alias route coverage.

## Prerequisites

- SB01 workbook regenerated and reviewed.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.ContractEndpoints.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.OperationsEndpoints.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`
- `repo://src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`

## Deliverables

- Focused test assertions for the missing high-risk routes.
- Source fixes if OpenAPI exposure and route registration disagree.
- Execution report proof with exact test command and outcome.

## Dependency Impact

- SB04 and SB05 depend on this phase because docs and skills must not claim routes that are not exposed or tested.
- SB06 can only encode drift guardrails after current API contract behavior is known.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Re-read the API Inventory workbook rows for Cognitive Memory.
2. Inspect current OpenAPI route test expectations.
3. Add missing assertions for Cognitive Memory legacy and v1 route families.
4. Run the focused integration test.
5. If the test fails because source registration is wrong, fix the route contract with the smallest source change and rerun.
6. Record test output and any route decisions in the execution report.

## Scope Exceptions

- Do not update docs or skills in this phase except to record discovered contract decisions in the execution report.
- Do not broaden the test to unrelated UI or database behavior unless required for route exposure proof.

## Do Not Do

- Do not skip v1 aliases.
- Do not weaken assertions to make stale docs pass.
- Do not silently ignore routes that fail OpenAPI exposure.

## Acceptance Checklist

- Missing Cognitive Memory route assertions are present.
- Focused API test has been run or a concrete environment blocker is recorded.
- Any source/OpenAPI mismatch is fixed or reopened before docs/skills work.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter Api_openapi_exposes_focused_control_plane_routes`
- Relevant diff in `tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs`.

## Browser Validation Logging

- `N/A`: this subbundle is API/test focused and does not change UI.

## Progression Gate

- SB04 and SB05 may proceed only after focused API route proof exists or a documented blocker is accepted in the execution report.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Use the workbook API Inventory and Cognitive Memory source files to repair focused route assertions. Run the focused integration test, record proof, and stop if OpenAPI exposure does not match source.
```
