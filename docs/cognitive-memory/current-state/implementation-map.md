# Implementation Map

## Project Shape

The implementation lives primarily in `src/CanDoItAll.Modules.CognitiveMemory`. It is registered from `src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` and exposed by grouped Minimal API files under `src/CanDoItAll.Web/Api/CognitiveMemoryApi*.cs`.

| Folder | Source files | Responsibility |
| --- | ---: | --- |
| `Advanced` | 15 | Probe sessions, feedback, self-model, calibration, self-regulation, professor review, answer gate, Epistemic Drive, cross-project promotion, distributed workers, MAF context contribution, and agent-facing context packaging. |
| `Common` | 4 | JSON context, provider contracts, EF guardrails, shared typed values. |
| `Consolidation` | 6 | Consolidation runs, candidates, deterministic fact extraction, candidate application into canonical records. |
| `Foundation` | 4 | Core records, source manifests/items/links, memory records, relations, runs, review items, projection states. |
| `Ingestion` | 4 | Source snapshot ingestion, layouts, graph links, context hints, tombstones, scan failures. |
| `Neuro` | 4 | Evidence anchors, claims, belief state, entity/context binding, mutation authority. |
| `Operations` | 4 | Explicit projection rebuild, scheduled automation runner, and retention cleanup contracts/services. |
| `Pages` | 17 | Blazor operator UI, page-specific CSS/code-behind, extracted rendering helpers, and ten tab child components. |
| `Procedural` | 4 | Procedure skills, steps, failure modes, simulation, validation evidence, automation bindings. |
| `Projection` | 2 | SemanticCompletion and RAG/Qdrant adapter contracts and implementations. |
| `Recall` | 15 | Recall orchestration, vector/workspace/signal/graph channels, candidate loading, scoring, evaluation, context packs, trace persistence, source references, internal types, and mapping helpers. |
| `ReviewUi` | 7 | Snapshot DTOs, summary queries, candidate previews, advanced query projections, trace/health queries, operator audit queries, and review decision workflow. |
| `Scoring` | 6 | Typed score spaces, dimensions, geometry driver, evaluation traces, persisted score components. |
| `Settings` | 8 | Automation settings, model access policy, model execution profiles, external file/web ingestion, staged manifests, external-source limits, and sensitive-content policy. |
| `Signals` | 4 | Prediction expectations, prediction errors, salience/signals, consumer policies. |
| `Taxonomy` | 4 | Record/relation validation, projection lifecycle, projection records. |
| `TemporalReplay` | 4 | Temporal episodes, replay jobs, causal links, worker results. |
| `Workspace` | 4 | Workspace frames, goals, slots, open questions, attention routing, inhibited candidates. |

## Runtime Registration

```mermaid
flowchart LR
    Program["CanDoItAll.Web Program.cs"] --> Infrastructure["AddCanDoItAllInfrastructure"]
    Infrastructure --> ModelRegistry["AppDbContextModelRegistry.ConfigureAssemblies"]
    Program --> Composition["AddCanDoItAllRuntimeModules"]
    Composition --> Qdrant["AddConfiguredQdrantRagDriver when enabled"]
    Composition --> Module["AddCognitiveMemoryModule"]
    Module --> Services["Cognitive Memory services"]
    Module --> Operations["Projection rebuild, automation runner, and retention cleanup"]
    Program --> Api["MapCanDoItAllApi"]
    Api --> CognitiveApi["MapCognitiveMemoryApi"]
    Program --> Razor["MapRazorComponents + module assemblies"]
    Razor --> Page["/cognitive-memory and /memory"]
```

## Core Services

| Service | Interface | Current role |
| --- | --- | --- |
| `CognitiveMemorySourceIngestionService` | `ICognitiveMemorySourceIngestionService` | Reads source snapshots and persists source records, evidence, layout, graph, context, tombstone, and failure state. |
| `CognitiveMemoryExternalSourceIngestionService` | `ICognitiveMemoryExternalSourceIngestionService` | Ingests uploaded files and web links into source manifests/items/evidence. |
| `CognitiveMemoryConsolidationEngine` | `ICognitiveMemoryConsolidationEngine` | Processes source items into candidates, mutation commands, review rows, canonical memory records, and projection invalidations. |
| `CognitiveMemoryConsolidationCandidateApplicator` | `ICognitiveMemoryConsolidationCandidateApplicator` | Materializes approved or machine-generated candidates into memory records, claims, source links, and evidence links. |
| `CognitiveMemoryRecallOrchestrator` | `ICognitiveMemoryRecallOrchestrator` | Builds recall candidates from lexical, optional vector, workspace, signal, graph, and source-detail channels. The orchestration is split across partial files by channel, loading, scoring, context-pack building, persistence, internal types, and mapping. |
| `CognitiveMemoryReviewUiService` | `ICognitiveMemoryReviewUiService` | Builds operator snapshots and applies review decisions through split summary, preview, advanced, trace, and health query files. |
| `CognitiveMemoryAgentContextContributor` | `IAgentContextContributor` | Adds agent-facing Cognitive Memory context packages to AgentFramework requests when provider policy and project scope allow it, and fails process-critical modes when required memory is unavailable. |
| `CognitiveMemorySignalLedger` | `ICognitiveMemorySignalLedger`, `ICognitiveMemoryPredictionErrorEngine` | Records prediction expectations, prediction errors, salience signals, scores, and consumer policies. |
| `CognitiveMemoryTemporalReplayService` | `ICognitiveMemoryTemporalEpisodeService`, `ICognitiveMemoryReplayScheduler` | Records temporal episodes and replay jobs. |
| `CognitiveMemoryProcedureSkillService` | `ICognitiveMemoryProcedureSkillMemoryService`, `ICognitiveMemorySimulationSandboxService` | Stores procedure skills and simulations. |
| `CognitiveMemoryAdvancedServices` classes | Several advanced interfaces | Own probing, self-model, calibration, self-regulation, answer gate, professor review, learning proposals, cross-project, and distributed coordination. |
| `CognitiveMemoryProjectionRebuildService` | `ICognitiveMemoryProjectionRebuildService` | Rebuilds stale/failed projection records from durable memory, source links, evidence anchors, claims, context frames, entity ids, and context-boundary policies through projection lifecycle. |
| `CognitiveMemoryScheduledAutomationRunner` | `ICognitiveMemoryScheduledAutomationRunner` | Honors automation schedule mode for explicit UI/API runs, triggers configured ingestion, and runs consolidation after successful ingestion when enabled. |
| `CognitiveMemoryRetentionCleanupService` | `ICognitiveMemoryRetentionCleanupService` | Deletes old operational rows through an explicit dry-run-first request while preserving canonical memory, source, evidence, and projection truth. |

## Operator UI Components

The `/cognitive-memory` route still uses `CognitiveMemoryPage` as the orchestration owner, but the tab bodies are separated under `src/CanDoItAll.Modules.CognitiveMemory/Pages/Components`:

| Component | Role |
| --- | --- |
| `CognitiveMemoryDashboardTab.razor` | Summary dashboard and review/health overview. |
| `CognitiveMemoryProbeWorkbenchTab.razor` | Probe session, voice-assisted question/correction flow, feedback, and source-backed answer context. |
| `CognitiveMemorySettingsTab.razor` | Automation settings, model policy, manual ingestion, explicit automation run, and projection rebuild controls. |
| `CognitiveMemorySourcesTab.razor` | External file and web-link ingestion. |
| `CognitiveMemoryMemoryTab.razor` | Memory explorer and source evidence detail. |
| `CognitiveMemoryReviewQueueTab.razor` | Review queue and decision panel. |
| `CognitiveMemoryRecallTracesTab.razor` | Recall trace, candidate, source, and context-pack detail. |
| `CognitiveMemoryHealthTab.razor` | Projection, consolidation, replay, procedure health, and operator audit. |
| `CognitiveMemorySelfRegulationTab.razor` | Self-regulation, answer-gate, professor-review, and learning surfaces. |
| `CognitiveMemoryScaleTab.razor` | Cross-project promotion and distributed worker/job surfaces. |

## Source Providers

| Source provider | Owner | Current source kind |
| --- | --- | --- |
| `WorkbenchProjectStructureSourceSnapshotProvider` | `CanDoItAll.Modules.Workbench` | `WorkbenchProjectStructure` |
| `ProcessRuntimeEvidenceSourceProvider` | `CanDoItAll.Modules.Processes` | `ProcessRuntime` |
| `WorkflowRuntimeEvidenceSourceProvider` | `CanDoItAll.Modules.AgentFramework` | `WorkflowRuntime` |
| `CognitiveMemoryExternalSourceIngestionService` | `CanDoItAll.Modules.CognitiveMemory` | Uploaded files and website links |

## HTTP Surface

The API currently maps 35 routes per surface under legacy `/api/cognitive-memory` and additive `/api/cognitive-memory/v1` route groups across these files:

| File | Responsibility |
| --- | --- |
| `CognitiveMemoryApi.cs` | Legacy/v1 route-group mapping, shared helpers, result/error normalization. |
| `CognitiveMemoryApi.ContractEndpoints.cs` | Contract version, route metadata, compatibility notes, and common-flow examples. |
| `CognitiveMemoryApi.DatabaseEndpoints.cs` | Status and database profile operations. |
| `CognitiveMemoryApi.SettingsEndpoints.cs` | Settings and model access policy. |
| `CognitiveMemoryApi.IngestionEndpoints.cs` | Project/process/external-source ingestion. |
| `CognitiveMemoryApi.RecallReviewEndpoints.cs` | Snapshot, generic source ingest, consolidation, recall, review decisions. |
| `CognitiveMemoryApi.OperationsEndpoints.cs` | Projection rebuild, explicit automation run, and retention cleanup operations. |
| `CognitiveMemoryApi.AdvancedEndpoints.cs` | Probe, self-regulation, answer gate, professor review, Epistemic Drive, cross-project operations. |
| `CognitiveMemoryApi.DistributedEndpoints.cs` | Distributed workers and jobs. |
| `CognitiveMemoryApiDtos.cs` | Request DTOs used by the endpoint groups. |

## Persistence Surface

The module currently has 109 entity record classes. Provider-specific migrations exist for both SQLite and PostgreSQL:

- SQLite migrations: 15 Cognitive Memory migrations.
- PostgreSQL migrations: 15 Cognitive Memory migrations.

This confirms the implementation is durable and provider-aware, but the schema should still be treated as alpha until the beta stabilization review closes.

## Test Surface

| Test project | Cognitive Memory files |
| --- | ---: |
| `CanDoItAll.Tests.Unit` | 17 |
| `CanDoItAll.Tests.Integration` | 12 |
| `CanDoItAll.Tests.Components` | 1 |
| `CanDoItAll.Tests.Playwright` | 1 |
| `CanDoItAll.Tests.Support` | 2 |

