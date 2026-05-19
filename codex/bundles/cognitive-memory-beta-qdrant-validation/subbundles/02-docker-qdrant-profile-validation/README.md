# Subbundle 02 - Docker Qdrant Profile Validation

## Status

- `Completed`

## Objective

- Validate that Docker Qdrant and PostgreSQL are healthy and that the web app is using the intended beta-capable Cognitive Memory provider configuration.
- Prove Qdrant gRPC/HTTP settings align with the running container before projection rebuild is executed.

## Covered Inputs

- CM-BETA-002: validate the Docker Qdrant runtime.
- CM-BETA-001: confirm P1 beta proof uses the real provider stack, not an in-memory fallback.

## Prerequisites

- Docker Desktop is running.
- `docker-compose.yml` services can be started without changing repository infrastructure.
- The app can be started on an available localhost port after builds/tests finish.

## Exact Source References

- `C:\repositories\CanDoItAll\docker-compose.yml`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Composition\RuntimeHostServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\docs\cognitive-memory\operations\provider-failure-runbook.md`

## Deliverables

- Docker container health proof for `candoitall-qdrant` and `candoitall-postgres`.
- Qdrant collection/config proof through the live container API.
- App status/profile proof through the Cognitive Memory API.

## Dependency Impact

- Runtime validation only unless profile selection or provider wiring is incorrect.
- If provider wiring is incorrect, code/config fixes must stay in composition/API boundaries.

## Validation Depth

- Use Docker health and Qdrant REST proof.
- Use app API status/contract/profile endpoints where available.
- Do not treat a silently skipped vector provider as success.

## Implementation Steps

1. Start or verify `qdrant` and `postgres` through Docker Compose.
2. Capture container health and port mapping evidence.
3. Query Qdrant REST for collections and the configured collection.
4. Start the web app after build/test-sensitive operations are complete.
5. Query Cognitive Memory status and contract endpoints.
6. Record all commands and results in the execution report.

## Do Not Do

- Do not use an in-memory test double for beta proof.
- Do not write directly to Cognitive Memory database tables.
- Do not ignore provider warnings from recall or projection operations.

## Acceptance Checklist

- Qdrant container is healthy and reachable at `127.0.0.1:6333`.
- Qdrant gRPC configuration points at `localhost:6334`.
- PostgreSQL container is healthy.
- App status endpoint reports a valid runtime profile.

## Proof Required

- Docker command output summary in the execution report.
- Qdrant REST response summary in the execution report.
- App API response summary in the execution report.

## Browser Validation Logging

- Browser proof is not required for container health.
- If the app health tab is used to cross-check runtime state, capture screenshots and console logs under `reviews/browser-proof`.

## Progression Gate

- Continue to projection rebuild only after Qdrant and PostgreSQL are healthy and the app API is reachable.

## Suggested Agent Prompt

```text
Validate the Docker Qdrant/PostgreSQL runtime and the app's Cognitive Memory provider settings. Capture concrete command/API proof and block projection validation if the vector provider is unavailable or silently skipped.
```
