# Cognitive Memory API

The Cognitive Memory HTTP API is hosted by `CanDoItAll.Web` under `/api/cognitive-memory`. The current implementation maps 33 endpoints across grouped files named `src/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs`.

## Status And Database Profile

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/cognitive-memory/status` | Returns active database profile summary and advertised endpoint list. |
| `GET` | `/api/cognitive-memory/database/selection` | Returns active profile selection. |
| `GET` | `/api/cognitive-memory/database/profiles` | Lists configured database profiles. |
| `POST` | `/api/cognitive-memory/database/profiles/postgresql` | Creates a PostgreSQL database profile and optionally switches to it. |
| `POST` | `/api/cognitive-memory/database/switch/{profileId}` | Switches the active profile. |

## Settings And Model Access

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/cognitive-memory/settings` | Reads automation settings, provider policy, default agent/provider, and model execution profiles. |
| `PUT` | `/api/cognitive-memory/settings` | Updates settings and model access policy. |

The model execution profile settings currently describe role-specific model preferences such as SourceIngestion, Consolidation, EpistemicDrive, Probe, and ProfessorReview. Do not assume these settings mean every role is already implemented as a live chat-model call. Current consolidation fact extraction is deterministic and source-backed.

## Ingestion

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/cognitive-memory/ingestion/project-structure` | Ingests Workbench project-structure snapshots. |
| `POST` | `/api/cognitive-memory/ingestion/processes` | Ingests process runtime evidence snapshots. |
| `POST` | `/api/cognitive-memory/external-sources/files` | Uploads an external file for source extraction. |
| `POST` | `/api/cognitive-memory/external-sources/web-links` | Ingests a web link as external source material. |
| `GET` | `/api/cognitive-memory/external-sources/ingestions/{operationId}` | Reads an external source ingestion operation. |
| `POST` | `/api/cognitive-memory/sources/ingest` | Generic source ingestion endpoint. |

Supported external extraction paths include text-like files, `.docx`, `.pptx`, `.xlsx`, and `.pdf`. Unsupported binary data should fail clearly rather than become fake text.

## Review, Consolidation, And Recall

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/cognitive-memory/snapshot` | Returns the operator/agent snapshot, with optional resolved-review history. |
| `POST` | `/api/cognitive-memory/consolidation/runs` | Runs consolidation over source items. |
| `POST` | `/api/cognitive-memory/recall` | Builds and persists a recall context pack and trace. |
| `POST` | `/api/cognitive-memory/review-items/{reviewItemId}/decisions` | Applies an operator review decision. |

## Operations

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/cognitive-memory/projections/rebuild` | Rebuilds stale or failed projection records from durable memory records and source/evidence links. |
| `POST` | `/api/cognitive-memory/automation/run` | Executes configured automation ingestion/consolidation when the supplied trigger is allowed by schedule settings. |

`/automation/run` is an explicit operational command. It honors persisted schedule settings, but it is not a hosted background scheduler by itself. Manual trigger is always allowed; nightly, idle-timeout, and scheduled-moment triggers run only when the persisted schedule mode matches.

`/projections/rebuild` rebuilds projection state from relational memory. Qdrant/RAG remains a projection target, not authoritative memory.

## Probing And Self-Regulation

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/cognitive-memory/probes/sessions` | Starts a probe session. |
| `POST` | `/api/cognitive-memory/probes/sessions/{sessionId}/turns` | Asks a probe question through recall. |
| `POST` | `/api/cognitive-memory/probes/turns/{turnId}/feedback` | Records feedback, correction, calibration, and learning signals. |
| `POST` | `/api/cognitive-memory/self-regulation/assessments` | Records self-regulation assessment. |
| `POST` | `/api/cognitive-memory/answer-gate/decisions` | Records answer-gate decision. |
| `POST` | `/api/cognitive-memory/professor-reviews` | Requests professor review. |
| `POST` | `/api/cognitive-memory/professor-reviews/{reviewId}/complete` | Completes professor review. |

## Learning, Cross-Project, And Distributed Work

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/cognitive-memory/epistemic-drive/scans` | Scans for source-backed learning opportunities. |
| `POST` | `/api/cognitive-memory/epistemic-drive/proposals/{proposalId}/decisions` | Approves, rejects, snoozes, or completes a learning proposal. |
| `POST` | `/api/cognitive-memory/cross-project/promotions` | Creates a cross-project promotion candidate. |
| `POST` | `/api/cognitive-memory/distributed/workers` | Registers or updates a distributed worker. |
| `POST` | `/api/cognitive-memory/distributed/jobs` | Enqueues a distributed job. |
| `POST` | `/api/cognitive-memory/distributed/jobs/claim` | Claims a distributed job lease. |
| `POST` | `/api/cognitive-memory/distributed/jobs/{jobId}/results` | Submits a distributed job result. |

## Operational Notes

- Use `GET /api/access/status` before API automation to confirm whether bearer tokens are required.
- Prefer PostgreSQL profiles for realistic multi-cycle memory validation.
- Do not treat `/snapshot` as the only proof. For memory quality, inspect recall traces, source refs, review decisions, and source truth.
- Keep agent-facing context separate from diagnostic candidate payloads when adding new API DTOs. MAF now uses an agent-facing `CognitiveMemoryAgentContextPackage`.

