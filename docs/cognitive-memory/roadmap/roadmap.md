# Cognitive Memory Roadmap

## Already Done

| Area | True status | Notes |
| --- | --- | --- |
| Module foundation | Done | Module project, DI registration, EF configuration discovery, PostgreSQL migrations. |
| Source ingestion | P1 beta for core path | Workbench, process, workflow, file, and web-link ingestion paths exist with provenance and evidence records. External source ingestion now has centralized limits, extraction error context, URL secret-query rejection, and sensitive-content rejection. Live beta proof ingested a Markdown source through the public file endpoint. |
| Score geometry | Done for alpha | Typed score spaces, evaluation traces, score components, and scalar projections exist. |
| Neuro foundation | Alpha complete | Evidence anchors, claims, belief state, entity/context binding, mutation commands, and audit records exist. |
| Consolidation | P1 beta for deterministic materialization | Candidates, review rows, mutation commands, and candidate application exist. Candidate application now creates context-frame/entity metadata required by projection rebuild. Extraction is still mostly deterministic/rule-based. |
| Recall | P1 beta for Qdrant-backed source recall | Lexical, optional vector, workspace, signal, graph, and source-detail channels feed persisted traces/context packs. Live proof shows public recall using `rag:qdrant:search:2`. |
| Review UI | P1 hardened alpha | Operator snapshot and decision path exist; approvals can materialize canonical memory. Health now includes operator audit signals for mutation commands/events, claim state, evidence anchors, projection failures, and retention cleanup runs. |
| API | P1 beta for local/agent automation | Legacy `/api/cognitive-memory` remains compatible. `/api/cognitive-memory/v1` aliases and `/contract` metadata/examples now expose the v1 contract. Recall and projection rebuild DTOs expose Qdrant projection settings. |
| MAF context | P0 alpha | Project-scoped context contribution exists with provider access policy, agent-facing context packaging, and explicit fail/skip behavior for process-critical modes. |
| Probes/self-regulation | Alpha | Probe sessions, feedback, calibration, answer gate, professor review, learning proposals, cross-project, and distributed records exist. |
| LB4U/live validation | Done for alpha | Previous bundle evidence validates realistic staged data with PostgreSQL and provider settings. |
| Projection rebuild | P1 beta with Docker Qdrant proof | Stale/failed projection records can be rebuilt explicitly through `ICognitiveMemoryProjectionRebuildService`, `/api/cognitive-memory/projections/rebuild`, and the operator settings tab. The rebuild reconstructs entity/boundary projection metadata from durable records, can project missing durable records, has adapter-backed RAG proof, deterministic provider-failure proof, and live Docker Qdrant proof. |
| Scheduled automation execution | P0 decision closed | `ICognitiveMemoryScheduledAutomationRunner` honors schedule mode and runs configured ingestion/consolidation through `/api/cognitive-memory/automation/run` and the operator settings tab. P0 deliberately keeps this explicit instead of adding hidden background mutation. |
| Maintainability split | P0 closed for roadmap scope | Advanced services, recall orchestration, recall channels, recall internal types, review UI queries/previews, API endpoint groups, DTOs, page code-behind, rendering helpers, and ten Razor child tabs were split. Broad beta hardening still needs further decomposition of older large service files. |
| Retention cleanup | P1 hardened alpha | `ICognitiveMemoryRetentionCleanupService` and `/api/cognitive-memory[/v1]/retention/cleanup` provide explicit dry-run-first cleanup for recall traces, rejected/duplicate candidates, closed probe sessions, and completed/rejected/expired distributed jobs. |

## Completed P0/P1 And Next Hardening

### P0 - Completed

1. Split oversized backend/API surfaces:
   - `CognitiveMemoryAdvancedServices.cs` was split into focused advanced service files.
   - `CognitiveMemoryRecallServices.cs` was split into partial files for channels, loading, scoring, evaluation, context-pack building, persistence, and mapping.
   - `CognitiveMemoryRecallChannels.cs` was split into vector, workspace/signal, and graph-expansion channel files.
   - `CognitiveMemoryRecallInternalTypes.cs` now owns internal recall DTO/type helpers.
   - `CognitiveMemoryReviewUiService.cs` was split into summary, candidate-preview, advanced, trace, and health query files.
   - `CognitiveMemoryApi.cs` was split into endpoint groups and `CognitiveMemoryApiDtos.cs`.
   - `CognitiveMemoryPage.razor.cs` was split into probe, settings/source operations, formatting, and rendering files.
   - `CognitiveMemoryPage.razor` now delegates tab bodies to ten child components under `Pages/Components`.
2. Added explicit projection rebuild:
   - consumes `CognitiveMemoryProjectionRecord.RebuildRequired`, `RebuildRequired` status, and failed projection rows;
   - rebuilds from durable memory records, source links, evidence anchors, claims, context frames, entities, and context-boundary policies;
   - calls the projection lifecycle service and persists item success/failure state;
   - exposes `/api/cognitive-memory/projections/rebuild`;
   - has adapter-backed proof through `RagCognitiveMemoryProjectionAdapter` with a recording `IRagDriver`.
3. Added explicit scheduled automation execution:
   - respects `CognitiveMemoryAutomationScheduleMode`;
   - triggers configured source ingestion and consolidation;
   - returns run summary and warnings;
   - exposes `/api/cognitive-memory/automation/run`;
   - appears in the operator settings tab as "Run automation";
   - does not introduce hidden background mutation.
4. Separated MAF agent context from diagnostic recall payloads with `CognitiveMemoryAgentContextPackage`.
5. Made MAF process-critical memory contribution fail predictably for governed process automation, auto-approved non-interactive runs, and A2A endpoint mode.

### P0 Closure Decisions Revalidated For Beta

1. P0 is closed for the roadmap scope and remains valid under the P1 beta proof.
2. Cognitive Memory does not add an autonomous hosted worker in P0. The current schedule model lacks a safe unattended project scope, so P0 uses explicit UI/API execution. A future hosted scheduler must define scope ownership, retry policy, idempotency, and operator audit first.
3. Adapter-backed projection proof is complete for P0. Live Qdrant/provider integration is now completed in P1 using Docker Qdrant and deterministic local hashing embeddings.
4. Large-file reduction continues in P1 for older broad services such as consolidation, settings, procedure, temporal replay, ingestion, workspace, and signal services.

### P1 - Completed Beta Pass

1. Versioned the HTTP API contract:
   - legacy `/api/cognitive-memory` remains compatible;
   - `/api/cognitive-memory/v1` maps additive v1 aliases with distinct endpoint names;
   - `/api/cognitive-memory/contract` and `/api/cognitive-memory/v1/contract` return route metadata and common-flow examples.
2. Added deterministic provider-failure proof and live Docker Qdrant proof:
   - projection rebuild failures now have unit proof for blocked run status, failed projection state, failure code/message, and rebuild-required recovery state;
   - the composition root registers deterministic local hashing embeddings when Qdrant is enabled;
   - missing durable records can be projected through `/api/cognitive-memory/v1/projections/rebuild`;
   - live validation projected 2/2 records into Docker Qdrant collection `candoitall-knowledge`;
   - public recall returned a completed vector stage with provider trace `rag:qdrant:search:2`.
3. Added explicit retention cleanup:
   - dry-run defaults to true;
   - cleanup targets recall traces and dependent operational rows, rejected/duplicate candidates, closed/abandoned probe sessions with turns/feedback, and completed/rejected/expired distributed jobs with results;
   - canonical memory records, claims, source manifests, source items, evidence anchors, and projection state are not deleted by default.
4. Added operator audit view:
   - `CognitiveMemoryReviewUiSnapshot.OperatorAudit` exposes mutation command, mutation audit event, claim state, evidence anchor, projection failure, and retention cleanup run signals;
   - the health tab renders the audit surface.
5. Hardened external source ingestion:
   - service-owned limits define 10 MB upload, 1,000,000 extracted text characters, 4,000-character chunks, and 80-character minimum chunks;
   - extraction failures include file/host context;
   - source text and URL query policy reject likely credentials without logging raw values.
6. Added performance baseline docs for large manifests and recall runs.
7. Continued decomposition by moving operator audit queries and external-source sensitive policy into focused files.
8. Revalidated P0 for beta:
   - P0 explicit projection rebuild remains the product path;
   - P0 explicit automation runner remains the intentional non-daemon decision;
   - P0 MAF context separation still holds;
   - P0 maintainability splits remain valid, with remaining large-service refactors moved to P2.

### P2 - Broaden And Operationalize The System

1. Move the Docker Qdrant validation into repeatable CI or an operator-owned scripted environment.
2. Add more source providers only after the existing source-provider boundary stays stable.
3. Expand cross-project promotion with stronger review and demotion workflows.
4. Turn distributed compute from alpha records into an operational worker model with signed job packets and result validation.
5. Add richer browser validation around review, probe, ingestion, projection rebuild, and retention flows.
6. Document stable operator runbooks for PostgreSQL-first memory validation and recovery.
7. Review the v1 HTTP surface as an external-client contract and decide which DTOs need generated-client compatibility guarantees.

## Beta Gate

Cognitive Memory is now beta for the core path because all of these are true:

- P0 closure decisions remain valid under live PostgreSQL/Qdrant validation.
- Projection rebuild is an ordinary service/API/UI product path, and missing durable records can be projected explicitly.
- Automation schedule settings cause observable, test-covered work, and the product decision about explicit runner versus hosted background worker is closed.
- Agent-facing context output is separate from diagnostic trace payloads.
- PostgreSQL validation has repeatable API/profile proof.
- UI pages have browser proof after component splits, including loaded dashboard and health tab at desktop/mobile viewports.
- The API contract is documented as a stable v1 local/agent surface.
- Live-provider release proof exists: Docker Qdrant collection `candoitall-knowledge`, 384-dimensional local hashing embeddings, 2/2 projected records, and public recall vector stage `rag:qdrant:search:2`.

The beta label does not mean every advanced surface is production-ready. Cross-project promotion, distributed compute, broad model-assisted consolidation, autonomous scheduling, and external-client SDK compatibility remain P2/P3 work.

