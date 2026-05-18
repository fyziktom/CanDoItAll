# Integration With Existing Project Code

## Module Registration

Add a new module assembly:

```text
CanDoItAll.Modules.CognitiveMemory
```

Expected registration path:

1. Add the module assembly to `ModuleAssemblies.All`.
2. Add service registration through an extension method similar to other modules.
3. Add EF configurations through the existing model registry pattern.
4. Register workflow executors through the existing workflow executor catalog.
5. Register MAF context provider/tool integration through the MAF adapter service registration.

## Proposed Project Structure

```text
src/
  CanDoItAll.CognitiveMemory.Abstractions/
  CanDoItAll.CognitiveMemory.Core/
  CanDoItAll.CognitiveMemory.Rag/
  CanDoItAll.CognitiveMemory.Semantics/
  CanDoItAll.CognitiveMemory.Maf/
  CanDoItAll.Modules.CognitiveMemory/
  CanDoItAll.Modules.CognitiveMemory.Components/
  CanDoItAll.Modules.CognitiveMemory.Tests/
```

Recommended responsibilities:

| Project | Responsibility |
|---|---|
| `Abstractions` | contracts usable by agents, workflows, plugins. |
| `Core` | recall, consolidation, clustering, activation, source canonicalization. |
| `Rag` | adapter over existing RAG/Qdrant driver. |
| `Semantics` | adapter over SemanticCompletion/embedding driver. |
| `Maf` | MAF context provider, tools, handoff helpers. |
| `Modules.CognitiveMemory` | EF entities, repositories, services, module registration. |
| `Components` | Blazor UI components. |
| `Tests` | unit/integration tests. |

## EF Integration

Current `AppDbContext` uses model configuration discovery. Cognitive Memory should add configuration classes such as:

```text
MemorySourceManifestRecordConfiguration
MemorySourceItemRecordConfiguration
CanonicalMemoryRecordConfiguration
MemoryItemRecordConfiguration
MemoryRelationRecordConfiguration
MemoryProjectionRecordConfiguration
MemoryRecallTraceRecordConfiguration
MemoryConsolidationRunRecordConfiguration
MemoryHumanReviewItemConfiguration
```

Do not directly modify `AppDbContext` unless existing registration requires it.

## Workbench Integration

Source ingestion should read from existing workbench records:

- `ProjectObjectRecord`
- `ProjectObjectLinkRecord`
- `ProjectStructureNode`

V1 compatibility:

- read `PositionX`, `PositionY`, and optional `z` from metadata,
- later migrate to explicit `PositionZ` if required.

## Process Integration

Use existing process entities as episodic sources:

- process run started/completed,
- step run result,
- process decision,
- artifact created,
- journal entry,
- conformance observation,
- improvement candidate.

Process completion should enqueue a reflection/consolidation job.

## Workflow Integration

Register new workflow executors:

```text
memory.source.ingest
memory.recall
memory.context.build
memory.consolidate
memory.project
memory.reflect
memory.review.enqueue
memory.procedure.extract
memory.qdrant.rebuild
```

These executors should be usable from workflow canvas nodes.

## RAG Integration

Existing RAG driver should be reused through an adapter:

```text
ICognitiveVectorProjectionStore -> IRagDriver
```

Required driver changes should be isolated behind a new adapter so the first implementation can work with existing functionality and evolve toward filters/named vectors.

## SemanticCompletion Integration

Existing semantic driver should be used for:

- embeddings,
- semantic ranking,
- intent classification,
- label classification,
- fallback local similarity.

Recommended adapter:

```text
ICognitiveEmbeddingProvider -> IAgentTextEmbeddingGenerator
```

## Storage Integration

Use existing storage services for:

- source snapshots,
- canonical bundles,
- consolidation reports,
- large recall context packs,
- review evidence packages.

Do not store large text blobs directly in memory tables if they exceed normal DB payload limits.

## Plugin Integration

Plugins are both:

1. source providers,
2. procedural execution tools.

Examples:

| Plugin | Cognitive Memory use |
|---|---|
| Docker | deployment/test environment evidence and procedures. |
| Gmail | email source ingestion and categorized correspondence. |
| Office365 | mail/calendar/document source ingestion. |
| GitHub planned | commits/issues/PR/repo source ingestion. |
| Jira planned | issue/task/process source ingestion. |

## Existing Simple Workspace Memory

The current `WorkspaceMemoryContextProvider` should not be deleted. It can be kept as:

- compatibility fallback,
- lexical context channel,
- baseline provider for tests,
- emergency mode when Cognitive Memory is disabled.

Cognitive Memory should eventually become the primary context provider.

## Migration Strategy

### Phase 1

Add module and models without changing existing workflows.

### Phase 2

Ingest workbench nodes and process/workflow runs into memory.

### Phase 3

Project into Qdrant and use recall manually.

### Phase 4

Inject recall context into MAF agents.

### Phase 5

Enable idle consolidation and human review.

### Phase 6

Enable distributed workers and cross-project semantic memory.

## Current Code Boundary Update

The supplied current code already implements:

- `IAgentContextContributor` and contribution traces,
- `IProjectStructureSourceSnapshotProvider`,
- `IProcessRuntimeEvidenceSourceProvider`,
- `IWorkflowRuntimeEvidenceSourceProvider`,
- Workbench, Process, and Workflow source snapshot providers,
- integration tests for snapshot determinism, cursor errors, redaction, restricted hashes, and z-index metadata.

Implementation agents should verify these are still present and then consume them. Do not duplicate source readers inside Cognitive Memory.

## Probing Integration With Existing Code

The probing module should reuse:

- MAF context contribution boundary for optional agent-visible probe context,
- workflow executor catalog for probe executors,
- Workbench/Process/Workflow source snapshot providers for source-grounded recall,
- existing review UI patterns for correction review,
- BaseLib/CanvasLib UI components for trace and graph presentation,
- storage services for large probe reports and regression evidence packages.
