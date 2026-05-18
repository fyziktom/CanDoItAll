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
- Check `GET /api/cognitive-memory/status` before behavior work. For consolidation, projection, or multi-cycle smoke tests, require an active PostgreSQL profile.
- For local projection work, make sure Docker Qdrant is running with `docker compose up -d qdrant`; the app expects Qdrant gRPC on `localhost:6334`.

## Database And Settings

- Status: `GET /api/cognitive-memory/status`.
- Active profile: `GET /api/cognitive-memory/database/selection`.
- Profiles: `GET /api/cognitive-memory/database/profiles`.
- Create PostgreSQL profile: `POST /api/cognitive-memory/database/profiles/postgresql`.
- Switch profile: `POST /api/cognitive-memory/database/switch/{profileId}`.
- Settings: `GET /api/cognitive-memory/settings`, `PUT /api/cognitive-memory/settings`.

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
- Review decisions: `POST /api/cognitive-memory/review-items/{reviewItemId}/decisions`.
- Probes: `POST /api/cognitive-memory/probes/sessions`, `POST /api/cognitive-memory/probes/sessions/{sessionId}/turns`, `POST /api/cognitive-memory/probes/turns/{turnId}/feedback`.
- Self-regulation: `POST /api/cognitive-memory/self-regulation/assessments`.
- Answer gate: `POST /api/cognitive-memory/answer-gate/decisions`.
- Professor review: `POST /api/cognitive-memory/professor-reviews`, `POST /api/cognitive-memory/professor-reviews/{reviewId}/complete`.
- Epistemic drive: `POST /api/cognitive-memory/epistemic-drive/scans`, `POST /api/cognitive-memory/epistemic-drive/proposals/{proposalId}/decisions`.
- Cross-project promotions: `POST /api/cognitive-memory/cross-project/promotions`.
- Distributed work: `POST /api/cognitive-memory/distributed/workers`, `/distributed/jobs`, `/distributed/jobs/claim`, and `/distributed/jobs/{jobId}/results`.

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
