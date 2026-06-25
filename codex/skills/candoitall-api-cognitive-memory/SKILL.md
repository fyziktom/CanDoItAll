---
name: candoitall-api-cognitive-memory
description: "Use when operating CanDoItAll Cognitive Memory through the HTTP API: checking status, PostgreSQL profile readiness, settings, source ingestion, external source ingestion, consolidation, recall, review decisions, probes, self-regulation, answer gate, professor review, epistemic-drive scans, cross-project promotions, distributed jobs, or Qdrant-backed projection validation."
---

# CanDoItAll Cognitive Memory API

Use this skill when a task needs Cognitive Memory control through the CanDoItAll web API. Do not write directly to Cognitive Memory tables, and do not call Qdrant directly for memory facts; Qdrant is only a rebuildable projection store.

## Access

- Start the CanDoItAll web app and inspect Swagger/OpenAPI at `/swagger`.
- Check `/api/access/status` before assuming bearer tokens are required.
- If JWT is active, send `Authorization: Bearer <token>`.
- Prefer `/api/cognitive-memory/v1` for new integrations. Legacy `/api/cognitive-memory` routes remain compatible aliases.
- Check `GET /api/cognitive-memory/v1/status` before behavior work. For consolidation, projection, or multi-cycle smoke tests, require an active PostgreSQL profile.
- Inspect `GET /api/cognitive-memory/v1/contract` before generating clients or route smoke checks. The current implementation maps 38 routes per surface.
- For local projection work, make sure Docker Qdrant is running with `docker compose up -d qdrant`; the app expects Qdrant gRPC on `localhost:6334`.

## Database And Settings

- Status: `GET /api/cognitive-memory/status`.
- Contract: `GET /api/cognitive-memory/contract`.
- Active profile: `GET /api/cognitive-memory/database/selection`.
- Profiles: `GET /api/cognitive-memory/database/profiles`.
- Transfer sources: `GET /api/cognitive-memory/database/transfer/sources/{targetProfileId}`.
- Transfer preview: `GET /api/cognitive-memory/database/transfer/preview`.
- Transfer execute: `POST /api/cognitive-memory/database/transfer`.
- Create PostgreSQL profile: `POST /api/cognitive-memory/database/profiles/postgresql`.
- Switch profile: `POST /api/cognitive-memory/database/switch/{profileId}`.
- Settings: `GET /api/cognitive-memory/settings`, `PUT /api/cognitive-memory/settings`.

The same routes exist under `/api/cognitive-memory/v1`; use that base path in new examples.

## Source Ingestion

- Project structure snapshot: `POST /api/cognitive-memory/ingestion/project-structure`.
- Process runtime snapshot: `POST /api/cognitive-memory/ingestion/processes`.
- Generic source ingestion: `POST /api/cognitive-memory/sources/ingest`.
- External files: `POST /api/cognitive-memory/external-sources/files` as `multipart/form-data`.
- External web links: `POST /api/cognitive-memory/external-sources/web-links`.
- External ingestion status: `GET /api/cognitive-memory/external-sources/ingestions/{operationId}`.

## Memory Operations

- Snapshot/review surface: `GET /api/cognitive-memory/snapshot`.
- Consolidation: `POST /api/cognitive-memory/consolidation/runs`.
- Recall: `POST /api/cognitive-memory/recall`.
- Projection rebuild: `POST /api/cognitive-memory/projections/rebuild`.
- Automation run: `POST /api/cognitive-memory/automation/run`.
- Retention cleanup: `POST /api/cognitive-memory/retention/cleanup`.
- Review decisions: `POST /api/cognitive-memory/review-items/{reviewItemId}/decisions`.
- Probes: `POST /api/cognitive-memory/probes/sessions`, `POST /api/cognitive-memory/probes/sessions/{sessionId}/turns`, `POST /api/cognitive-memory/probes/turns/{turnId}/feedback`.
- Self-regulation: `POST /api/cognitive-memory/self-regulation/assessments`.
- Answer gate: `POST /api/cognitive-memory/answer-gate/decisions`.
- Professor review: `POST /api/cognitive-memory/professor-reviews`, `POST /api/cognitive-memory/professor-reviews/{reviewId}/complete`.
- Epistemic drive: `POST /api/cognitive-memory/epistemic-drive/scans`, `POST /api/cognitive-memory/epistemic-drive/proposals/{proposalId}/decisions`.
- Cross-project promotions: `POST /api/cognitive-memory/cross-project/promotions`.
- Distributed work: `POST /api/cognitive-memory/distributed/workers`, `/distributed/jobs`, `/distributed/jobs/claim`, and `/distributed/jobs/{jobId}/results`.

## DTO Checkpoints

`CognitiveMemorySettingsApiRequest` fields:

- `isEnabled`
- `scheduleMode`
- `nightlyLocalTime`
- `idleMinutes`
- `scheduledLocalTimes`
- `autoIngestProjectStructure`
- `autoIngestProcessRuntime`
- `autoConsolidateAfterIngestion`
- `modelAccessMode`
- `defaultProviderProfileId`
- `defaultAgentId`
- `allowedProviderProfileIds`
- `modelExecutionProfiles`
- `actorId`

`CognitiveMemoryProjectionRebuildApiRequest` fields: `projectId`, `take`, `actorId`, `collectionName`, `projectMissingRecords`, `projectionProfileId`, `embeddingProfileId`, `targetProviderName`, `projectionStoreKind`, `vectorDimensions`.

`CognitiveMemoryAutomationRunApiRequest` fields: `projectId`, `triggerKind`, `actorId`, `take`, `cycleId`, `maxCycles`, `continueUntilIdle`, `policy`.

`CognitiveMemoryRetentionCleanupApiRequest` fields: `projectId`, `deleteBeforeUtc`, `dryRun`, `scopes`, `actorId`. `dryRun` defaults to true.

## Operating Rules

- Prefer focused endpoints over database inspection.
- Use idempotency keys for repeatable ingestion and consolidation runs.
- For project-scoped work, pass `projectId` consistently through ingestion, consolidation, probes, and recall.
- Treat provider-unavailable errors as useful diagnostics. Do not hide missing embedding, ranking, or projection-provider errors.
- Keep Qdrant projection validation separate from truth validation: durable records, claims, evidence, review items, and traces live in the app database.

## Validation

- After database profile creation or switch, read back `/api/cognitive-memory/status`.
- After ingestion, read the operation result and then query `/api/cognitive-memory/snapshot`.
- After consolidation, read snapshot/review items and run recall with a small focused budget.
- After projection-sensitive work, verify Docker Qdrant is healthy and that recall either uses vector projection or records a clear projection-provider warning.

## Source Route Appendix

<!-- api-docs-skills-parity:routes:start -->

Cognitive Memory API route appendix. Generated from Minimal API registrations; rerun `.codex/tmp/api-docs-skills-gap-map/update-skill-route-appendices.mjs` when routes change.

| Method | Route |
| --- | --- |
| `POST` | `/api/cognitive-memory/answer-gate/decisions` |
| `POST` | `/api/cognitive-memory/automation/run` |
| `POST` | `/api/cognitive-memory/consolidation/runs` |
| `GET` | `/api/cognitive-memory/contract` |
| `POST` | `/api/cognitive-memory/cross-project/promotions` |
| `GET` | `/api/cognitive-memory/database/profiles` |
| `POST` | `/api/cognitive-memory/database/profiles/postgresql` |
| `GET` | `/api/cognitive-memory/database/selection` |
| `POST` | `/api/cognitive-memory/database/switch/{profileId:guid}` |
| `POST` | `/api/cognitive-memory/database/transfer` |
| `GET` | `/api/cognitive-memory/database/transfer/preview` |
| `GET` | `/api/cognitive-memory/database/transfer/sources/{targetProfileId:guid}` |
| `POST` | `/api/cognitive-memory/distributed/jobs` |
| `POST` | `/api/cognitive-memory/distributed/jobs/{jobId:guid}/results` |
| `POST` | `/api/cognitive-memory/distributed/jobs/claim` |
| `POST` | `/api/cognitive-memory/distributed/workers` |
| `POST` | `/api/cognitive-memory/epistemic-drive/proposals/{proposalId:guid}/decisions` |
| `POST` | `/api/cognitive-memory/epistemic-drive/scans` |
| `POST` | `/api/cognitive-memory/external-sources/files` |
| `GET` | `/api/cognitive-memory/external-sources/ingestions/{operationId:guid}` |
| `POST` | `/api/cognitive-memory/external-sources/web-links` |
| `POST` | `/api/cognitive-memory/ingestion/processes` |
| `POST` | `/api/cognitive-memory/ingestion/project-structure` |
| `POST` | `/api/cognitive-memory/probes/sessions` |
| `POST` | `/api/cognitive-memory/probes/sessions/{sessionId:guid}/turns` |
| `POST` | `/api/cognitive-memory/probes/turns/{turnId:guid}/feedback` |
| `POST` | `/api/cognitive-memory/professor-reviews` |
| `POST` | `/api/cognitive-memory/professor-reviews/{reviewId:guid}/complete` |
| `POST` | `/api/cognitive-memory/projections/rebuild` |
| `POST` | `/api/cognitive-memory/recall` |
| `POST` | `/api/cognitive-memory/retention/cleanup` |
| `POST` | `/api/cognitive-memory/review-items/{reviewItemId:guid}/decisions` |
| `POST` | `/api/cognitive-memory/self-regulation/assessments` |
| `GET` | `/api/cognitive-memory/settings` |
| `PUT` | `/api/cognitive-memory/settings` |
| `GET` | `/api/cognitive-memory/snapshot` |
| `POST` | `/api/cognitive-memory/sources/ingest` |
| `GET` | `/api/cognitive-memory/status` |
| `POST` | `/api/cognitive-memory/v1/answer-gate/decisions` |
| `POST` | `/api/cognitive-memory/v1/automation/run` |
| `POST` | `/api/cognitive-memory/v1/consolidation/runs` |
| `GET` | `/api/cognitive-memory/v1/contract` |
| `POST` | `/api/cognitive-memory/v1/cross-project/promotions` |
| `GET` | `/api/cognitive-memory/v1/database/profiles` |
| `POST` | `/api/cognitive-memory/v1/database/profiles/postgresql` |
| `GET` | `/api/cognitive-memory/v1/database/selection` |
| `POST` | `/api/cognitive-memory/v1/database/switch/{profileId:guid}` |
| `POST` | `/api/cognitive-memory/v1/database/transfer` |
| `GET` | `/api/cognitive-memory/v1/database/transfer/preview` |
| `GET` | `/api/cognitive-memory/v1/database/transfer/sources/{targetProfileId:guid}` |
| `POST` | `/api/cognitive-memory/v1/distributed/jobs` |
| `POST` | `/api/cognitive-memory/v1/distributed/jobs/{jobId:guid}/results` |
| `POST` | `/api/cognitive-memory/v1/distributed/jobs/claim` |
| `POST` | `/api/cognitive-memory/v1/distributed/workers` |
| `POST` | `/api/cognitive-memory/v1/epistemic-drive/proposals/{proposalId:guid}/decisions` |
| `POST` | `/api/cognitive-memory/v1/epistemic-drive/scans` |
| `POST` | `/api/cognitive-memory/v1/external-sources/files` |
| `GET` | `/api/cognitive-memory/v1/external-sources/ingestions/{operationId:guid}` |
| `POST` | `/api/cognitive-memory/v1/external-sources/web-links` |
| `POST` | `/api/cognitive-memory/v1/ingestion/processes` |
| `POST` | `/api/cognitive-memory/v1/ingestion/project-structure` |
| `POST` | `/api/cognitive-memory/v1/probes/sessions` |
| `POST` | `/api/cognitive-memory/v1/probes/sessions/{sessionId:guid}/turns` |
| `POST` | `/api/cognitive-memory/v1/probes/turns/{turnId:guid}/feedback` |
| `POST` | `/api/cognitive-memory/v1/professor-reviews` |
| `POST` | `/api/cognitive-memory/v1/professor-reviews/{reviewId:guid}/complete` |
| `POST` | `/api/cognitive-memory/v1/projections/rebuild` |
| `POST` | `/api/cognitive-memory/v1/recall` |
| `POST` | `/api/cognitive-memory/v1/retention/cleanup` |
| `POST` | `/api/cognitive-memory/v1/review-items/{reviewItemId:guid}/decisions` |
| `POST` | `/api/cognitive-memory/v1/self-regulation/assessments` |
| `GET` | `/api/cognitive-memory/v1/settings` |
| `PUT` | `/api/cognitive-memory/v1/settings` |
| `GET` | `/api/cognitive-memory/v1/snapshot` |
| `POST` | `/api/cognitive-memory/v1/sources/ingest` |
| `GET` | `/api/cognitive-memory/v1/status` |

<!-- api-docs-skills-parity:routes:end -->
