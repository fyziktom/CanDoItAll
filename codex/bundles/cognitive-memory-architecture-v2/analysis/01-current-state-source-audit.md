# Current State Source Audit

## Main CanDoItAll Solution

Inspected root: `CanDoItAll-development`.

### Composition and Module Loading

Relevant files:

- `src/CanDoItAll.Composition/ModuleAssemblies.cs`
- `src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`
- `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

Findings:

- Modules are composed through `AddCanDoItAllRuntimeModules(...)`.
- EF model configurations are discovered through `AppDbContextModelRegistry.ConfigureAssemblies(moduleAssemblies)`.
- A new Cognitive Memory module can be added like existing modules without changing the core `AppDbContext` directly.
- The module should provide its own `IEntityTypeConfiguration<T>` classes and be added to `ModuleAssemblies.All`.

### Infrastructure and Storage

Relevant files:

- `src/CanDoItAll.Infrastructure/Storage/Abstractions/StorageContracts.cs`
- `src/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `src/CanDoItAll.Infrastructure/Storage/Drivers/IpfsStorageDriver.cs`
- `src/CanDoItAll.Infrastructure/Storage/Drivers/FtpStorageDriver.cs`
- `src/CanDoItAll.Infrastructure/Storage/Placement/StoragePlacementService.cs`
- `src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs`

Findings:

- The existing storage layer already supports FileSystem, IPFS, and FTP.
- `IStoragePlacementService` can store large source snapshots, canonical bundles, generated summaries, and consolidation artifacts.
- `ISearchIndexService` provides a simple relational keyword search surface. It should be reused as lexical recall channel, not replaced by Qdrant.
- IPFS is already treated as a content-addressed storage option and is a good fit for immutable memory artifacts.

### Project Workbench / Mindmap-Like Structure

Relevant files:

- `src/CanDoItAll.Modules.Workbench/Workbench/ProjectWorkbenchModels.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectNodes/ProjectNodeBindings.cs`
- `src/CanDoItAll.SharedKernel/Projects/ProjectObjectContracts.cs`
- `src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentModels.cs`

Findings:

- `ProjectObjectRecord` stores project nodes with:
  - `ProjectId`
  - `NodeKey`
  - `ObjectType`
  - `Title`
  - `Subtitle`
  - `Notes`
  - `MetadataJson`
  - `ParentNodeKey`
  - `PositionX`
  - `PositionY`
- `ProjectObjectLinkRecord` stores typed links such as `Contains`, `DependsOn`, `Uses`, `Validates`, `Tests`, `Blocks`, `DerivedFrom`, and `BelongsTo`.
- `ProjectStructureNode` exposes `X` and `Y` in the surface model.
- Current source appears to support 2D coordinates in this module. The user wants X/Y/Z. Target design therefore adds either:
  - a migration with `PositionZ`, or
  - a compatibility fallback reading `z` from `MetadataJson` until migration exists.
- Existing object types include `Repository`, `File`, `Environment`, `Infrastructure`, `PromptFlow`, `ProcessDefinition`, `WorkflowDefinition`, `ValidationRun`, `TestPlan`, `TestEvidence`, `Note`, `Decision`, and `SecretReference`. These are excellent source categories for memory ingestion.

### Microsoft Agent Framework Integration

Relevant files:

- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowContracts.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`

Findings:

- The MAF adapter references:
  - `Microsoft.Agents.AI` version `1.3.0`
  - `Microsoft.Agents.AI.Workflows` version `1.3.0`
  - `Microsoft.Agents.AI.A2A` preview
  - `Microsoft.Agents.AI.Mem0` preview
  - `OllamaSharp`
- `ContextCapabilityBuilder` already supports:
  - a local `TextSearchProvider` over files,
  - static context messages,
  - Mem0 provider attachment.
- `WorkspaceMemoryContextProvider` currently performs simple relevance scoring using keyword occurrence and memory importance. This is a good V0 precedent but too shallow for the requested cognitive memory behavior.
- Workflow infrastructure already has:
  - `IWorkflowExecutor`,
  - `IWorkflowExecutorCatalog`,
  - `IWorkflowRuntimeManager`,
  - `IWorkflowRunStore`,
  - persistent workflow catalog/run stores,
  - executor nodes,
  - artifacts,
  - external requests,
  - run events.
- Cognitive Memory should integrate as:
  - MAF context provider,
  - workflow executors,
  - agent tools,
  - consolidation workflows,
  - process reflection hooks.

### Process Runtime / Episodes / Artifacts

Relevant files:

- `src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`
- `src/CanDoItAll.Modules.Processes/Runtime/*`

Findings:

- Existing runtime entities already provide rich episodic inputs:
  - `ProcessRun`
  - `ProcessStepRun`
  - `ProcessRunAssignment`
  - `ProcessWorkBrief`
  - `ProcessDecisionRecord`
  - `ProcessArtifactRecord`
  - `ProcessJournalEntry`
  - `ProcessConformanceObservation`
  - `ProcessImprovementCandidate`
  - `ProcessWorkflowRunLink`
- These should become first-class sources for episodic memory, procedural memory, decision memory, and reflective memory.
- Process artifacts already store provenance and managed storage paths. Cognitive Memory should not duplicate large content; it should reference existing artifact paths and source records.

### Automation and Scheduling

Relevant files:

- `src/CanDoItAll.Modules.Automation/Services/AutomationModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerModuleServiceCollectionExtensions.cs`

Findings:

- Quartz is already integrated.
- Automation has a message dispatcher, trigger registry, telemetry publisher, and background hosted workers.
- Cognitive Memory consolidation should use this automation layer instead of inventing an unrelated scheduler.

### Plugins

Relevant files:

- `src/CanDoItAll.Plugins.Abstractions/PluginExecutionContracts.cs`
- `src/CanDoItAll.Modules.Plugins/Services/PluginsModuleServiceCollectionExtensions.cs`
- `src/plugins/CanDoItAll.Plugin.Docker/*`
- `src/plugins/CanDoItAll.Plugin.Gmail/*`
- `src/plugins/CanDoItAll.Plugin.Office365/*`

Findings:

- Plugin workflow executors already exist and can be used as source-ingestion channels.
- Plugin capability context already exposes workspace files, storage, project structure, HTTP, OAuth2, execution events, and host tools.
- Gmail/Office365/Docker plugins can become source providers for emails, labels/categories, Docker environment evidence, and workflow event traces.

## Standalone RAG Repository

Inspected root: `CanDoItAll.AgentFramework.Rag-main`.

Relevant files:

- `src/CanDoItAll.AgentFramework.Rag.Driver/Abstractions/IRagDriver.cs`
- `src/CanDoItAll.AgentFramework.Rag.Driver/Embeddings/IRagEmbeddingGenerator.cs`
- `src/CanDoItAll.AgentFramework.Rag.Driver/Models/RagKnowledgeEntry.cs`
- `src/CanDoItAll.AgentFramework.Rag.Driver/Models/RagCollectionOptions.cs`
- `src/CanDoItAll.AgentFramework.Rag.Qdrant/QdrantRagDriver.cs`
- `src/CanDoItAll.AgentFramework.Rag.Qdrant/Mapping/QdrantRagMapper.cs`

Findings:

- Provider-neutral RAG driver exists.
- Qdrant implementation exists.
- Current driver supports one unnamed vector per collection, tags, typed filters, payload index requests, delete-by-filter cleanup, and capability discovery.
- Current `RagSearchRequest` carries a provider-neutral filter tree.
- Current Qdrant mapping stores metadata as payload and preserves reserved keys for knowledge id/text/tags.
- Current Qdrant mapping translates typed filters and lifecycle cleanup filters to Qdrant payload filters.

Remaining extensions for Cognitive Memory:

- collection/point schema versioning,
- optional named vectors or a compatible multi-collection fallback,
- search by IDs/source refs,
- projection adapter policies over the generic delete-by-filter and payload-index contracts.

## Standalone SemanticCompletion Repository

Inspected root: `CanDoItAll.AgentFramework.SemanticCompletion-main`.

Relevant files:

- `src/CanDoItAll.AgentFramework.SemanticCompletion.Driver/Embeddings/IAgentTextEmbeddingGenerator.cs`
- `src/CanDoItAll.AgentFramework.SemanticCompletion.Driver/Embeddings/OnnxAgentTextEmbeddingGenerator.cs`
- `src/CanDoItAll.AgentFramework.SemanticCompletion.Driver/Semantics/SemanticTextRanker.cs`
- `src/CanDoItAll.AgentFramework.SemanticCompletion.Driver/Semantics/SemanticClassifier.cs`
- `src/CanDoItAll.AgentFramework.SemanticCompletion.Driver/Vectors/VectorSimilarity.cs`

Findings:

- This module provides local ONNX embeddings and deterministic local hashing fallback.
- Embedding results include stable profile metadata for provider/model/profile identity, dimension, normalization, tokenizer, and max-token signals.
- `SemanticTextRanker` and `SemanticClassifier<TLabel>` are useful for semantic ranking, classification, and fallback intent checks.
- It should not become the memory source of truth.
- It should be wrapped as an embedding provider for the Cognitive Memory projection engine and as a classifier for recall intent/projection-type decisions.

## Main Reuse Map

| Existing component | Use in Cognitive Memory |
|---|---|
| `AppDbContextModelRegistry` | Add Cognitive Memory EF model configurations through module assembly registration. |
| `IStoragePlacementService` | Store canonical bundles, source snapshots, consolidation reports, and large context packs. |
| `ISearchIndexService` | Lexical recall channel and UI search bridge. |
| `ProjectObjectRecord` / `ProjectObjectLinkRecord` | Mindmap/project graph source input. |
| `ProjectStructureRuntimeGateway` | Agent/tool access to project structure sources. |
| `ProcessRun`, `ProcessStepRun`, `ProcessDecisionRecord`, `ProcessArtifactRecord`, `ProcessJournalEntry` | Episodic and reflective memory source input. |
| `IWorkflowExecutor` | Add memory recall/consolidation/projection executors. |
| `IWorkflowRunStore` / workflow events | Workflow episodic memory source input. |
| `MafAgentRuntime` context provider mechanism | Inject recall context packs into MAF agent runs. |
| Plugin workflow executor model | Add source ingestion from Gmail/Office365/GitHub/Jira/etc. |
| RAG `IRagDriver` / Qdrant driver | Vector projection store. |
| SemanticCompletion embedding generator | Local ONNX embedding provider adapter. |
| Automation/Quartz | Idle/night consolidation scheduling. |

## Key Gaps

1. No durable source manifest for knowledge sources and source items.
2. No canonical memory model.
3. No explicit memory relation graph.
4. No multi-stage recall orchestrator.
5. No Cognitive Memory projection manager or payload schema yet; generic RAG typed filters and delete-by-filter are now available, while named vectors remain optional/future.
6. No mindmap feature extractor using spatial + graph + semantic signals.
7. No idle/night consolidation run model.
8. No activation/staleness/confidence model.
9. No contradiction/supersession/human-review queue.
10. No distributed compute job protocol.
11. No MAF context provider that builds progressive recall context packs.
12. No workflow executors for recall, consolidation, projection, source ingestion, reflection.
