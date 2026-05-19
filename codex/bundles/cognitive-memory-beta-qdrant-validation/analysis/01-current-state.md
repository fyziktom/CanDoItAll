# Current State

The previous P1 bundle closed local beta-hardening work but deliberately left Cognitive Memory at **P1-complete beta-candidate alpha** because live Qdrant/provider validation had not been executed.

Current source-grounded state:

- The app maps legacy `/api/cognitive-memory` and additive `/api/cognitive-memory/v1` routes.
- `GET /api/cognitive-memory/v1/contract` exposes contract metadata and examples.
- `ICognitiveMemoryProjectionRebuildService` can rebuild stale/failed projection rows from durable memory records.
- Qdrant is configured in `appsettings.json` and Docker currently has a healthy `candoitall-qdrant` container.
- PostgreSQL is available in Docker and should be preferred for beta validation.
- P0 implemented explicit projection rebuild, explicit automation execution, MAF context separation, stricter process-critical context failure, and large surface splits.
- P1 implemented API versioning, provider failure proof, retention cleanup, operator audit, external-source hardening, and docs.

Open beta question:

- Does the actual Docker Qdrant path work end-to-end through the app/API with durable memory data, or does P0/P1 need additional repair before beta?

