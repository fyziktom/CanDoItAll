# 01-api-foundation-auth-swagger

## Status

- `Completed`

## Objective

Add the API foundation: OpenAPI/Swagger metadata, optional JWT bearer authorization, strongly typed options, token issuer, route-group auth helper, and default `appsettings.json` section.

## Covered Inputs

- N001 API with Swagger and optional JWT.
- N002 Reuse logic by creating only HTTP/auth helpers, not business behavior.
- N008 JWT disabled by default with Settings token creation later.

## Prerequisites

- Prepared bundle validator passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.json`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.Manager\Program.cs`

## Deliverables

- `ApiOptions` and validation.
- JWT bearer registration only when authorization is enabled.
- Token issuer service for Settings UI.
- OpenAPI mapping.
- Route-group helper for conditional authorization.

## Dependency Impact

- Subbundles 02 and 03 both depend on this foundation. Weak proof here would invalidate endpoint security and token UI behavior.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add OpenAPI and JWT-related package references to the web project.
2. Add options and token issuer types.
3. Register services and middleware in `Program.cs` with correct auth ordering.
4. Map OpenAPI metadata.
5. Add default `Api` configuration with auth disabled.

## Scope Exceptions

- Swagger UI package is not mandatory if OpenAPI metadata is available through the framework endpoint.

## Do Not Do

- Do not add business endpoints in this subbundle.
- Do not store issued tokens.
- Do not log tokens or signing keys.

## Acceptance Checklist

- OpenAPI endpoint is mapped.
- App starts when JWT is disabled.
- JWT enabled without signing key fails predictably.
- Conditional authorization helper can protect route groups when enabled.

## Proof Required

- Targeted unit tests for options/token issuer.
- Targeted integration test for anonymous access in disabled/enabled modes.
- `dotnet build src/CanDoItAll.Web/CanDoItAll.Web.csproj`.

## Browser Validation Logging

- N/A. API foundation has no browser-visible UI.

## Progression Gate

- Downstream work may start only after auth/OpenAPI build and tests pass or exact blockers are recorded.

## Suggested Agent Prompt

```text
Implement only the API foundation, optional JWT configuration, OpenAPI mapping, and token issuer. Do not add project/process/agent endpoints yet.
```
