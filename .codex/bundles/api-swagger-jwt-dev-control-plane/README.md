# API Swagger JWT Development Control Plane

This bundle is a coordination and execution package for `api-swagger-jwt-dev-control-plane`.

## Profile

- `initiative`

## Mission

Add a documented development API to the Blazor host that exposes projects, project-structure operations, process definition/runtime control, launch-plan HR matching, and agent catalog/execution surfaces. The API must publish Swagger/OpenAPI metadata, optionally require JWT bearer authorization from `appsettings.json`, and keep endpoint handlers thin by reusing the existing UI/MCP/agent services.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-api-foundation-auth-swagger`
2. `subbundles/02-02-project-process-agent-api-surface`
3. `subbundles/03-03-settings-token-ui`
4. `subbundles/04-04-tests-proof-architecture-review`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Completed`
- Final closure gate: `Completed`
- Browser validation analytics: `Completed with HTTP route smoke`
