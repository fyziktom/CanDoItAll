# 03-clean-postgres-qdrant-environment

## Status

- `Completed`

## Objective

Determine whether a clean PostgreSQL-backed Cognitive Memory profile and Qdrant projection are available for realistic validation, and activate them when safe.

## Covered Inputs

- REQ-06, REQ-08, REQ-09.

## Prerequisites

- Web app can start.
- Cognitive Memory API status endpoint is reachable.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.DatabaseEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.SettingsEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.OperationsEndpoints.cs`

## Deliverables

- API status proof.
- Database profile inventory proof.
- PostgreSQL/Qdrant readiness proof or explicit blocker.
- Clean-profile activation proof if available.

## Dependency Impact

- Source-truth transfer and long-running memory validation depend on a clear storage target.

## Validation Depth

- Environment-critical.

## Implementation Steps

1. Start or reuse the web app.
2. Call `/api/access/status` and `/api/cognitive-memory/status`.
3. Inspect database profiles and active profile.
4. Inspect Docker/Qdrant readiness.
5. Create/switch a clean PostgreSQL profile only if credentials and service readiness are discoverable.

## Do Not Do

- Do not delete or reset an existing user database without an explicit safe target.
- Do not claim Qdrant proof from database status alone.
- Do not write Cognitive Memory facts directly to tables.

## Acceptance Checklist

- API status is captured.
- Active database profile is captured.
- PostgreSQL readiness is proven or blocked with cause.
- Qdrant readiness is proven or blocked with cause.

## Proof Required

- API JSON captures under `proof/api`.
- Terminal/docker readiness output under `proof/api` or execution report.

## Browser Validation Logging

- N/A unless database status UI is inspected.

## Progression Gate

- If clean storage is blocked, subbundles 04 and 05 continue only as partial validation and must record the blocker.

## Suggested Agent Prompt

```text
Execute subbundle 03. Use the app API to inspect Cognitive Memory database status and local service readiness. Activate a clean PostgreSQL/Qdrant path only when safe; otherwise record blockers.
```
