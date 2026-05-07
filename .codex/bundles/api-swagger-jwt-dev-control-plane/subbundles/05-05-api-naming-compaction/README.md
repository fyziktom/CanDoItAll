# 05-api-naming-compaction

## Status

- `Completed`

## Objective

Remove `Development` from API type names, method names, operation names, configuration names, tests, and user-facing labels introduced by this bundle.

## Covered Inputs

- Correction item 1: use concise API names such as `ListProjects` instead of verbose development-prefixed names.

## Prerequisites

- Subbundles 01-04 exist and are understood.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\ApiAccess`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ApiIntegrationTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.json`

## Deliverables

- Concise API naming in web endpoint files, workspace API access services, tests, Settings labels, and OpenAPI operation names.
- Routes remain documented and JWT behavior remains unchanged.

## Dependency Impact

- Subbundles 06-08 depend on this so new endpoints do not continue the rejected naming pattern.

## Validation Depth

- Critical correction hygiene.

## Implementation Steps

1. Rename introduced files, types, route mapper methods, options, token service types, tests, and operation names.
2. Change app configuration and Settings labels away from `Development` naming.
3. Update integration tests and route smoke paths.
4. Search introduced API code for remaining rejected names.

## Do Not Do

- Do not rename unrelated product concepts that predate the API bundle.
- Do not preserve compatibility aliases that keep the rejected `Development` names alive unless a direct existing client dependency is proven.

## Acceptance Checklist

- Introduced API files, types, mapper methods, operation names, tests, Settings labels, and configuration names no longer use `Development`.
- API behavior remains equivalent after the rename.
- Routes and tests use concise names.

## Proof Required

- Source search showing introduced API code no longer uses `Development` names.
- Targeted build and integration tests.

## Proof Captured

- Renamed API files/types/options/token services/route mappers from development-prefixed names to concise `Api*`, `ProjectsApi`, `ProcessesApi`, and `AgentsApi` names.
- Changed the route group from the old dev-scoped API path to `/api` and configuration to `Api`.
- Changed Settings UI tab/labels to `API Access`.
- Source search over introduced API files found no rejected old API names or routes.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -v:minimal` passed.

## Browser Validation Logging

- Settings route smoke is sufficient unless visual layout changes.

## Progression Gate

- Downstream command expansion may start only after build/test/source search shows no introduced API `Development` naming remains.

## Suggested Agent Prompt

```text
Rename the introduced API code away from Development terminology. Keep behavior equivalent and update tests/routes/configuration.
```
