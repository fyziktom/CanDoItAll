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
| Projection rebuild | P0 alpha | Stale/failed projection records can be rebuilt explicitly through `ICognitiveMemoryProjectionRebuildService` and `/api/cognitive-memory/projections/rebuild`. |
| Scheduled automation execution | P0 alpha | `ICognitiveMemoryScheduledAutomationRunner` honors schedule mode and runs configured ingestion/consolidation through `/api/cognitive-memory/automation/run`. |
| Maintainability split | P0 partial | Advanced services, recall orchestration, API endpoint groups, DTOs, and page rendering helpers were split. Razor markup and several large helper files remain. |

## Next Steps To Beta

### P0 - Completed In This Pass

1. Split oversized backend/API surfaces:
   - `CognitiveMemoryAdvancedServices.cs` was split into focused advanced service files.
   - `CognitiveMemoryRecallServices.cs` was split into partial files for channels, loading, scoring, evaluation, context-pack building, persistence, and mapping.
   - `CognitiveMemoryApi.cs` was split into endpoint groups and `CognitiveMemoryApiDtos.cs`.
   - `CognitiveMemoryPage.razor.cs` had rendering helpers extracted into `CognitiveMemoryPage.Rendering.cs`.
2. Added explicit projection rebuild:
   - consumes `CognitiveMemoryProjectionRecord.RebuildRequired`, `RebuildRequired` status, and failed projection rows;
   - rebuilds from durable memory records, source links, evidence anchors, claims, and context frames;
   - calls the projection lifecycle service and persists item success/failure state;
   - exposes `/api/cognitive-memory/projections/rebuild`.
3. Added explicit scheduled automation execution:
   - respects `CognitiveMemoryAutomationScheduleMode`;
   - triggers configured source ingestion and consolidation;
   - returns run summary and warnings;
   - exposes `/api/cognitive-memory/automation/run`;
   - does not introduce hidden background mutation.
4. Separated MAF agent context from diagnostic recall payloads with `CognitiveMemoryAgentContextPackage`.
5. Made MAF process-critical memory contribution fail predictably for governed process automation, auto-approved non-interactive runs, and A2A endpoint mode.

### P0 Residuals

1. Split `CognitiveMemoryPage.razor` into focused child components. This was not done in P0 because only render-helper extraction was needed to keep the implementation safe and non-behavioral.
2. Decide whether Cognitive Memory needs a hosted scheduler. The current state is explicit/API-triggered automation; that is safer than silent background mutation, but it is not an autonomous worker.
3. Continue reducing large residual files such as `CognitiveMemoryPage.razor.cs`, `CognitiveMemoryRecallChannels.cs`, `CognitiveMemoryRecallMappingAndTypes.cs`, and `CognitiveMemoryReviewUiService`.
4. Add provider-backed projection integration proof against the real RAG/Qdrant path.

### P1 - Stabilize Product Behavior

1. Version the HTTP API contract and add examples for common flows.
2. Add projection provider, scheduler/hosted-worker if introduced, and provider-failure integration tests.
3. Add retention/cleanup policy for traces, candidates, probe turns, and distributed jobs.
4. Add operator audit views for mutation commands, claim/evidence changes, and projection rebuild failures.
5. Harden external source ingestion with clearer size limits, extraction error details, and secret/sensitive content policy.
6. Add performance baselines for large manifests and recall runs.

### P2 - Broaden The System

1. Add more source providers only after the existing source-provider boundary stays stable.
2. Expand cross-project promotion with stronger review and demotion workflows.
3. Turn distributed compute from alpha records into an operational worker model with signed job packets and result validation.
4. Add richer browser validation around the refactored operator UI.
5. Document stable operator runbooks for PostgreSQL-first memory validation and recovery.

## Release Gate For Beta

Cognitive Memory should not be called beta until all of these are true:

- Remaining P0 residuals are closed or consciously moved to a beta hardening issue with owner and proof.
- Projection rebuild is an ordinary product path, not only a lifecycle service.
- Automation schedule settings cause observable, test-covered work, and the product decision about explicit runner versus hosted background worker is closed.
- Agent-facing context output is separate from diagnostic trace payloads.
- PostgreSQL validation has a repeatable script/runbook.
- UI pages have browser proof after component splits.
- The API contract is documented as a stable versioned surface.

