# Cognitive Memory API

> Status: historical native Cognitive Memory API documentation. The current base-host memory surface is the generic Memory Provider runtime and `/memory` UI documented in [provider setup](provider-setup.md). Use this page for retained native-provider history only unless a future native service API document explicitly supersedes it.

The Cognitive Memory HTTP API is hosted by `CanDoItAll.Web` under two route surfaces:

- legacy compatibility: `/api/cognitive-memory`
- additive v1 aliases: `/api/cognitive-memory/v1`

Both surfaces map the same operational behavior. The legacy routes remain for existing callers; new callers should prefer the v1 base path and inspect `GET /contract` before automation. The current implementation maps 38 routes per surface across grouped files named `src/App/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs`.

## Contract And Status

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/cognitive-memory/contract` | `/api/cognitive-memory/v1/contract` | Returns contract version, base path, route metadata, and common-flow examples. |
| `GET` | `/api/cognitive-memory/status` | `/api/cognitive-memory/v1/status` | Returns active database profile summary, contract version/path, and advertised route list. |

The contract version is currently `v1`. Endpoint names are distinct between the legacy and v1 surfaces so both route groups can coexist in one Minimal API host.

## Status And Database Profile

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/cognitive-memory/database/selection` | `/api/cognitive-memory/v1/database/selection` | Returns active profile selection. |
| `GET` | `/api/cognitive-memory/database/profiles` | `/api/cognitive-memory/v1/database/profiles` | Lists configured database profiles. |
| `GET` | `/api/cognitive-memory/database/transfer/sources/{targetProfileId}` | `/api/cognitive-memory/v1/database/transfer/sources/{targetProfileId}` | Lists transfer source profiles for the target profile. |
| `POST` | `/api/cognitive-memory/database/transfer/preview` | `/api/cognitive-memory/v1/database/transfer/preview` | Previews database-profile transfer impact. |
| `POST` | `/api/cognitive-memory/database/transfer` | `/api/cognitive-memory/v1/database/transfer` | Executes a database-profile transfer. |
| `POST` | `/api/cognitive-memory/database/profiles/postgresql` | `/api/cognitive-memory/v1/database/profiles/postgresql` | Creates a PostgreSQL database profile and optionally switches to it. |
| `POST` | `/api/cognitive-memory/database/switch/{profileId}` | `/api/cognitive-memory/v1/database/switch/{profileId}` | Switches the active profile. |

## Settings And Model Access

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/cognitive-memory/settings` | `/api/cognitive-memory/v1/settings` | Reads automation settings, provider policy, default agent/provider, and model execution profiles. |
| `PUT` | `/api/cognitive-memory/settings` | `/api/cognitive-memory/v1/settings` | Updates settings and model access policy. |

The model execution profile settings describe role-specific model preferences such as SourceIngestion, Consolidation, EpistemicDrive, Probe, and ProfessorReview. Do not assume these settings mean every role is already implemented as a live chat-model call. Current consolidation fact extraction is deterministic and source-backed.

`CognitiveMemorySettingsApiRequest` carries `isEnabled`, `scheduleMode`, `nightlyLocalTime`, `idleMinutes`, `scheduledLocalTimes`, `autoIngestProjectStructure`, `autoIngestProcessRuntime`, `autoConsolidateAfterIngestion`, `modelAccessMode`, `defaultProviderProfileId`, `defaultAgentId`, `allowedProviderProfileIds`, `modelExecutionProfiles`, and `actorId`.

## Ingestion

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/cognitive-memory/ingestion/project-structure` | `/api/cognitive-memory/v1/ingestion/project-structure` | Ingests Workbench project-structure snapshots. |
| `POST` | `/api/cognitive-memory/ingestion/processes` | `/api/cognitive-memory/v1/ingestion/processes` | Ingests process runtime evidence snapshots. |
| `POST` | `/api/cognitive-memory/external-sources/files` | `/api/cognitive-memory/v1/external-sources/files` | Uploads an external file for source extraction. |
| `POST` | `/api/cognitive-memory/external-sources/web-links` | `/api/cognitive-memory/v1/external-sources/web-links` | Ingests a web link as external source material. |
| `GET` | `/api/cognitive-memory/external-sources/ingestions/{operationId}` | `/api/cognitive-memory/v1/external-sources/ingestions/{operationId}` | Reads an external source ingestion operation. |
| `POST` | `/api/cognitive-memory/sources/ingest` | `/api/cognitive-memory/v1/sources/ingest` | Generic source ingestion endpoint. |

Supported external extraction paths include text-like files, `.docx`, `.pptx`, `.xlsx`, and `.pdf`. Uploads are capped by `CognitiveMemoryExternalSourceIngestionLimits.MaxFileBytes`. Extracted text is capped before source/evidence rows are created. Likely credentials and sensitive URL query parameters are rejected explicitly; see [external source policy](external-source-policy.md).

## Review, Consolidation, And Recall

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `GET` | `/api/cognitive-memory/snapshot` | `/api/cognitive-memory/v1/snapshot` | Returns the operator/agent snapshot, with optional resolved-review history and operator audit signals. |
| `POST` | `/api/cognitive-memory/consolidation/runs` | `/api/cognitive-memory/v1/consolidation/runs` | Runs consolidation over source items. |
| `POST` | `/api/cognitive-memory/recall` | `/api/cognitive-memory/v1/recall` | Builds and persists a recall context pack and trace, with optional vector projection settings. |
| `POST` | `/api/cognitive-memory/review-items/{reviewItemId}/decisions` | `/api/cognitive-memory/v1/review-items/{reviewItemId}/decisions` | Applies an operator review decision. |

## Operations

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/cognitive-memory/projections/rebuild` | `/api/cognitive-memory/v1/projections/rebuild` | Rebuilds stale or failed projection records from durable memory records and source/evidence links. |
| `POST` | `/api/cognitive-memory/automation/run` | `/api/cognitive-memory/v1/automation/run` | Executes configured automation ingestion/consolidation when the supplied trigger is allowed by schedule settings. |
| `POST` | `/api/cognitive-memory/retention/cleanup` | `/api/cognitive-memory/v1/retention/cleanup` | Runs explicit dry-run-first cleanup for operational records. |

`/automation/run` is an explicit operational command and is also exposed from the Cognitive Memory settings tab. It honors persisted schedule settings, but it is not a hosted background scheduler by itself. Manual trigger is always allowed; nightly, idle-timeout, and scheduled-moment triggers run only when the persisted schedule mode matches.

`/projections/rebuild` rebuilds projection state from relational memory and is also exposed from the Cognitive Memory settings tab. The rebuild path reconstructs projection payloads from memory records, claims, evidence anchors, source links, context frames, entity ids, and context-boundary policies. It can also project missing durable records when `projectMissingRecords` is true and the request or configured defaults provide `collectionName`, `projectionProfileId`, `embeddingProfileId`, `targetProviderName`, `projectionStoreKind`, and `vectorDimensions`. Qdrant/RAG remains a projection target, not authoritative memory. Provider failures leave rows failed and rebuildable.

`/retention/cleanup` defaults to dry-run. It can delete old recall traces, rejected/duplicate consolidation candidates, closed probe sessions, and completed/rejected/expired distributed jobs. It does not delete canonical memory records, source manifests, source items, claims, evidence anchors, or projection state; see [retention cleanup](retention-cleanup.md).

Request DTO checkpoints:

- `CognitiveMemoryProjectionRebuildApiRequest`: `projectId`, `take`, `actorId`, `collectionName`, `projectMissingRecords`, `projectionProfileId`, `embeddingProfileId`, `targetProviderName`, `projectionStoreKind`, `vectorDimensions`.
- `CognitiveMemoryAutomationRunApiRequest`: `projectId`, `triggerKind`, `actorId`, `take`, `cycleId`, `maxCycles`, `continueUntilIdle`, `policy`.
- `CognitiveMemoryRetentionCleanupApiRequest`: `projectId`, `deleteBeforeUtc`, `dryRun`, `scopes`, `actorId`. `dryRun` defaults to true.

## Probing And Self-Regulation

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/cognitive-memory/probes/sessions` | `/api/cognitive-memory/v1/probes/sessions` | Starts a probe session. |
| `POST` | `/api/cognitive-memory/probes/sessions/{sessionId}/turns` | `/api/cognitive-memory/v1/probes/sessions/{sessionId}/turns` | Asks a probe question through recall. |
| `POST` | `/api/cognitive-memory/probes/turns/{turnId}/feedback` | `/api/cognitive-memory/v1/probes/turns/{turnId}/feedback` | Records feedback, correction, calibration, and learning signals. |
| `POST` | `/api/cognitive-memory/self-regulation/assessments` | `/api/cognitive-memory/v1/self-regulation/assessments` | Records self-regulation assessment. |
| `POST` | `/api/cognitive-memory/answer-gate/decisions` | `/api/cognitive-memory/v1/answer-gate/decisions` | Records answer-gate decision. |
| `POST` | `/api/cognitive-memory/professor-reviews` | `/api/cognitive-memory/v1/professor-reviews` | Requests professor review. |
| `POST` | `/api/cognitive-memory/professor-reviews/{reviewId}/complete` | `/api/cognitive-memory/v1/professor-reviews/{reviewId}/complete` | Completes professor review. |

## Learning, Cross-Project, And Distributed Work

| Method | Legacy route | V1 route | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/cognitive-memory/epistemic-drive/scans` | `/api/cognitive-memory/v1/epistemic-drive/scans` | Scans for source-backed learning opportunities. |
| `POST` | `/api/cognitive-memory/epistemic-drive/proposals/{proposalId}/decisions` | `/api/cognitive-memory/v1/epistemic-drive/proposals/{proposalId}/decisions` | Approves, rejects, snoozes, or completes a learning proposal. |
| `POST` | `/api/cognitive-memory/cross-project/promotions` | `/api/cognitive-memory/v1/cross-project/promotions` | Creates a cross-project promotion candidate. |
| `POST` | `/api/cognitive-memory/distributed/workers` | `/api/cognitive-memory/v1/distributed/workers` | Registers or updates a distributed worker. |
| `POST` | `/api/cognitive-memory/distributed/jobs` | `/api/cognitive-memory/v1/distributed/jobs` | Enqueues a distributed job. |
| `POST` | `/api/cognitive-memory/distributed/jobs/claim` | `/api/cognitive-memory/v1/distributed/jobs/claim` | Claims a distributed job lease. |
| `POST` | `/api/cognitive-memory/distributed/jobs/{jobId}/results` | `/api/cognitive-memory/v1/distributed/jobs/{jobId}/results` | Submits a distributed job result. |

## Operational Notes

- Use `GET /api/access/status` before API automation to confirm whether bearer tokens are required.
- Prefer PostgreSQL profiles for realistic multi-cycle memory validation.
- Prefer `/api/cognitive-memory/v1` for new automation and use `/contract` to generate clients or smoke checks.
- For Qdrant-backed recall, include `projectionCollectionName`, `projectionProfileId`, and `embeddingProfileId` in the recall request. A successful vector proof must show a recall stage with provider trace such as `rag:qdrant:search:2`; a skipped vector stage is not provider proof.
- For live Docker Qdrant projection, the current beta proof uses collection `candoitall-knowledge`, projection profile `qdrant-default-v1`, embedding profile `local-hashing-v1:dimension=384`, target provider `qdrant`, store kind `Qdrant`, and vector dimension `384`.
- Do not treat `/snapshot` as the only proof. For memory quality, inspect recall traces, source refs, review decisions, operator audit, and source truth.
- Keep agent-facing context separate from diagnostic candidate payloads when adding new API DTOs. MAF uses an agent-facing `CognitiveMemoryAgentContextPackage`.

## Qdrant Beta Request Examples

Projection rebuild for missing durable records:

```json
{
  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "take": 10,
  "actorId": "operator:projection-rebuild",
  "collectionName": "candoitall-knowledge",
  "projectMissingRecords": true,
  "projectionProfileId": "qdrant-default-v1",
  "embeddingProfileId": "local-hashing-v1:dimension=384",
  "targetProviderName": "qdrant",
  "projectionStoreKind": "Qdrant",
  "vectorDimensions": 384
}
```

Qdrant-backed recall:

```json
{
  "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "query": "Which source-backed facts must be considered?",
  "intent": "SourceLookup",
  "mode": "DeepSourceGrounded",
  "projectionCollectionName": "candoitall-knowledge",
  "projectionProfileId": "qdrant-default-v1",
  "embeddingProfileId": "local-hashing-v1:dimension=384",
  "budget": {
    "coarseCandidateLimit": 24,
    "vectorResultLimit": 12,
    "focusLimit": 8,
    "detailItemLimit": 8,
    "contextCharacterBudget": 12000,
    "maxSourceBytes": 24000
  }
}
```
