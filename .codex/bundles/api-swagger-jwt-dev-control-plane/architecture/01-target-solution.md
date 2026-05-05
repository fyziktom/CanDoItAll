# Target Solution

## Design

- Add `CanDoItAll.Web.Api` feature files under `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api`.
- Register OpenAPI with the same framework package used by `CanDoItAll.Manager`.
- Add `ApiOptions` with `SectionName = "Api"` and nested JWT settings. Default `Enabled = true` for the API surface and `Authorization.Enabled = false`.
- Add a token issuer service that signs JWTs only when authorization is enabled and the signing key passes validation.
- Add a route-group helper that applies `.RequireAuthorization()` only when optional JWT is active.
- Add typed route groups:
  - `/api/projects`
  - `/api/processes`
  - `/api/agents`
  - existing `/api/project-structure-mcp`
- Keep `ProjectStructureAgentApi` mapped and included in the auth/OpenAPI story.

## Boundaries

- HTTP boundary: routing, auth, ProblemDetails/error mapping, filtering DTOs, and OpenAPI metadata.
- Application boundary: reuse `ProjectsService`, `ProcessesService`, `ProjectStructureAgentService`, and `IAgentFrameworkWorkspaceService`.
- UI boundary: Settings tab reads auth options and calls token issuer; it does not mutate appsettings or persist tokens.
- Security boundary: JWT tokens are generated from configured signing material; signing key values are never echoed to UI/logs.

## Validation Strategy

- Unit tests for JWT options and token issuer.
- Integration tests with the web host endpoint groups mapped against test services.
- Process filtering test seeded through `ProcessDevelopmentSeedService`.
- Build `src/CanDoItAll.Web/CanDoItAll.Web.csproj`.
- Settings UI browser/component proof if the app can launch.
