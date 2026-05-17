# 01-developer-api-and-skill

## Status

- `Completed`

## Objective

Add a developer HTTP API and local Codex skill so Cognitive Memory can be controlled during development without direct database writes.

## Success Criteria

- `/api/cognitive-memory` routes are mapped.
- OpenAPI exposes the routes.
- The skill exists and tells Codex to verify PostgreSQL before testing.
- Web project builds.

## Covered Inputs

- R3 Developer API.
- R4 Codex skill.

## Prerequisites

- Subbundle `00-current-state-and-postgres-gate` policy is accepted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\ApiEndpointRouteBuilderExtensions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ApiIntegrationTests.cs`
- `C:\Users\lucys\.codex\skills\candoitall-api-cognitive-memory\SKILL.md`

## Deliverables

- Status, snapshot, ingestion, consolidation, recall, and review-decision endpoints.
- OpenAPI route assertions.
- Local skill for cognitive-memory API usage.

## Dependency Impact

- The PostgreSQL smoke depends on these endpoints and skill guardrails.

## Validation Depth

- API control-plane foundation.

## Implementation Steps

1. Add API endpoint mapping.
2. Add request DTOs that map to typed Cognitive Memory contracts.
3. Add OpenAPI route assertions.
4. Install skill under Codex skills.
5. Build web project.

## Scope Exceptions

- Does not add an MCP server.
- Does not add UI.

## Do Not Do

- Do not direct-reference Cognitive Memory from Web if it causes duplicate static-web-assets.
- Do not swallow missing provider errors.

## Acceptance Checklist

- API compiles.
- Skill has PostgreSQL guardrails.
- OpenAPI test knows the routes.

## Proof Required

- `dotnet build .\src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore`
- Focused integration test after API mapping.

## Browser Validation Logging

- N/A.

## Progression Gate

- Behavior smoke can start only after the skill is installed and API build succeeds.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add the smallest developer API over existing Cognitive Memory services, install the skill, build, and do not add direct database control paths.
```
