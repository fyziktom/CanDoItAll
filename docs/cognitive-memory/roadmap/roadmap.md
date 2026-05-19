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
| API | Alpha | Broad Minimal API exists and was validated, but contract shape still needs stabilization. |
| MAF context | Alpha | Project-scoped context contribution exists with provider access policy. |
| Probes/self-regulation | Alpha | Probe sessions, feedback, calibration, answer gate, professor review, learning proposals, cross-project, and distributed records exist. |
| LB4U/live validation | Done for alpha | Previous bundle evidence validates realistic staged data with PostgreSQL and provider settings. |

## Next Steps To Beta

### P0 - Make The Alpha Maintainable

1. Split oversized services by use case:
   - `CognitiveMemoryRecallServices.cs` into candidate loading, vector channel, graph expansion, scoring, context rendering, and persistence.
   - `CognitiveMemoryAdvancedServices.cs` into probe, self-model, calibration, self-regulation, professor review, answer gate, learning, cross-project, and distributed services/files.
   - `CognitiveMemoryPage.razor` and `.razor.cs` into focused child components backed by services.
   - `CognitiveMemoryApi.cs` into endpoint groups and DTO files.
2. Add a projection rebuild worker or explicit API command:
   - consume `CognitiveMemoryProjectionRecord.RebuildRequired`;
   - call projection lifecycle service;
   - persist success/failure and provider traces;
   - prove Qdrant/RAG points are rebuildable from durable memory.
3. Implement real scheduled automation:
   - respect `CognitiveMemoryAutomationScheduleMode`;
   - trigger source ingestion and consolidation;
   - log run records and failures;
   - prove no hidden background mutation of canonical truth.
4. Separate agent context DTOs from diagnostic recall DTOs.
5. Make MAF memory contribution fail/skip policy explicit for process-critical agent runs.

### P1 - Stabilize Product Behavior

1. Version the HTTP API contract and add examples for common flows.
2. Add projection, scheduler, and provider-failure integration tests.
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

- P0 refactors are complete and targeted tests still pass.
- Projection rebuild is an ordinary product path, not only a lifecycle service.
- Automation schedule settings cause observable, test-covered work.
- Agent-facing context output is separate from diagnostic trace payloads.
- PostgreSQL validation has a repeatable script/runbook.
- UI pages have browser proof after component splits.
- The API contract is documented as a stable versioned surface.

