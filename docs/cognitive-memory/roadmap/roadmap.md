# Cognitive Memory Roadmap

## Already Done

| Area | True status | Notes |
| --- | --- | --- |
| Module foundation | Done | Module project, DI registration, EF configuration discovery, SQLite/PostgreSQL migrations. |
| Source ingestion | Alpha complete | Workbench, process, workflow, file, and web-link ingestion paths exist with provenance and evidence records. |
| Score geometry | Done for alpha | Typed score spaces, evaluation traces, score components, and scalar projections exist. |
| Neuro foundation | Alpha complete | Evidence anchors, claims, belief state, entity/context binding, mutation commands, and audit records exist. |
| Consolidation | Alpha | Candidates, review rows, mutation commands, and candidate application exist. Extraction is still mostly deterministic/rule-based. |
| Recall | Alpha complete | Lexical, optional vector, workspace, signal, graph, and source-detail channels feed persisted traces/context packs. |
| Review UI | Alpha complete | Operator snapshot and decision path exist; approvals can materialize canonical memory. |
| API | P0 alpha | Broad Minimal API exists, is split into endpoint groups/DTOs, and was validated. Contract versioning still needs stabilization. |
| MAF context | P0 alpha | Project-scoped context contribution exists with provider access policy, agent-facing context packaging, and explicit fail/skip behavior for process-critical modes. |
| Probes/self-regulation | Alpha | Probe sessions, feedback, calibration, answer gate, professor review, learning proposals, cross-project, and distributed records exist. |
| LB4U/live validation | Done for alpha | Previous bundle evidence validates realistic staged data with PostgreSQL and provider settings. |
| Projection rebuild | P0 closed alpha | Stale/failed projection records can be rebuilt explicitly through `ICognitiveMemoryProjectionRebuildService`, `/api/cognitive-memory/projections/rebuild`, and the operator settings tab. The rebuild now reconstructs entity/boundary projection metadata from durable records and has adapter-backed RAG unit proof. |
| Scheduled automation execution | P0 decision closed | `ICognitiveMemoryScheduledAutomationRunner` honors schedule mode and runs configured ingestion/consolidation through `/api/cognitive-memory/automation/run` and the operator settings tab. P0 deliberately keeps this explicit instead of adding hidden background mutation. |
| Maintainability split | P0 closed for roadmap scope | Advanced services, recall orchestration, recall channels, recall internal types, review UI queries/previews, API endpoint groups, DTOs, page code-behind, rendering helpers, and ten Razor child tabs were split. Broad beta hardening still needs further decomposition of older large service files. |

## Next Steps To Beta

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

### P0 Closure Decisions

1. P0 is closed for the roadmap scope, but the module remains alpha because beta requires API contract stabilization, operational runbooks, and broader product hardening.
2. Cognitive Memory does not add an autonomous hosted worker in P0. The current schedule model lacks a safe unattended project scope, so P0 uses explicit UI/API execution. A future hosted scheduler must define scope ownership, retry policy, idempotency, and operator audit first.
3. Adapter-backed projection proof is complete for P0. Live Qdrant/provider integration remains a P1 beta-hardening item because it depends on environment configuration and failure runbooks.
4. Large-file reduction continues in P1 for older broad services such as consolidation, settings, procedure, temporal replay, ingestion, workspace, and signal services.

### P1 - Stabilize Product Behavior

1. Version the HTTP API contract and add examples for common flows.
2. Add live Qdrant/provider projection tests, provider-failure integration tests, and hosted-worker tests only if a scoped worker is introduced.
3. Add retention/cleanup policy for traces, candidates, probe turns, and distributed jobs.
4. Add operator audit views for mutation commands, claim/evidence changes, and projection rebuild failures.
5. Harden external source ingestion with clearer size limits, extraction error details, and secret/sensitive content policy.
6. Add performance baselines for large manifests and recall runs.
7. Continue decomposing older large services beyond the P0 surfaces without changing public behavior.

### P2 - Broaden The System

1. Add more source providers only after the existing source-provider boundary stays stable.
2. Expand cross-project promotion with stronger review and demotion workflows.
3. Turn distributed compute from alpha records into an operational worker model with signed job packets and result validation.
4. Add richer browser validation around the refactored operator UI.
5. Document stable operator runbooks for PostgreSQL-first memory validation and recovery.

## Release Gate For Beta

Cognitive Memory should not be called beta until all of these are true:

- P0 closure decisions remain valid under production-like validation.
- Projection rebuild is an ordinary product path, not only a lifecycle service.
- Automation schedule settings cause observable, test-covered work, and the product decision about explicit runner versus hosted background worker is closed.
- Agent-facing context output is separate from diagnostic trace payloads.
- PostgreSQL validation has a repeatable script/runbook.
- UI pages have browser proof for key operator workflows after component splits.
- The API contract is documented as a stable versioned surface.

