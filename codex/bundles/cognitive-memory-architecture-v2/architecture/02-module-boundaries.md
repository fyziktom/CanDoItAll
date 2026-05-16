# Module Boundaries And Integration Points

## New Module Boundary

`CanDoItAll.Modules.CognitiveMemory` should be a normal CanDoItAll runtime module. It should depend on existing abstractions but avoid forcing existing modules to depend on Cognitive Memory.

Recommended dependency direction after the prerequisite boundary contracts are validated:

```text
CanDoItAll.CognitiveMemory.Abstractions
    <- source snapshot adapters from Workbench/Processes/Workflows
    <- MAF context contribution adapter

CanDoItAll.CognitiveMemory.Core
    -> CanDoItAll.CognitiveMemory.Abstractions
    -> storage/search/security abstractions

CanDoItAll.CognitiveMemory.Rag
    -> CanDoItAll.CognitiveMemory.Abstractions
    -> CanDoItAll.AgentFramework.Rag.Driver

CanDoItAll.CognitiveMemory.Semantics
    -> CanDoItAll.CognitiveMemory.Abstractions
    -> CanDoItAll.AgentFramework.SemanticCompletion.Driver

CanDoItAll.Modules.CognitiveMemory
    -> CanDoItAll.CognitiveMemory.Core
    -> CanDoItAll.CognitiveMemory.Rag
    -> CanDoItAll.CognitiveMemory.Semantics
    -> source snapshot contracts
```

Avoid reverse dependencies from Projects/Workbench/Processes into CognitiveMemory. Existing modules may implement source snapshot contracts, but they must not know about canonical memory records, recall traces, Qdrant projection state, or consolidation policy.

## Registration

Add marker:

```csharp
public static class CognitiveMemoryModuleAssemblyMarker;
```

Add service extension:

```csharp
public static IServiceCollection AddCognitiveMemoryModule(
    this IServiceCollection services,
    IConfiguration configuration)
```

Add to:

- `ModuleAssemblies.All`
- `RuntimeHostServiceCollectionExtensions.AddCanDoItAllRuntimeModules(...)`

## EF Model Registration

Use the existing `AppDbContextModelRegistry`. The module must include `IEntityTypeConfiguration<T>` classes. No direct `DbSet<T>` properties are required on `AppDbContext`.

## MAF Integration Boundary

Do not add Cognitive Memory directly to the private MAF context builder. Consume the existing general MAF context contribution boundary from the target branch. If that boundary is missing in a branch, stop and re-run the prerequisite boundary bundle before Cognitive Memory work continues.

Add MAF-facing services without changing the durable memory core:

- `CognitiveMemoryContextProviderFactory`
- `CognitiveMemoryWorkflowExecutors`
- `MemoryRecallToolFactory`
- `ProcessReflectionHook`

The MAF runtime should consume memory through interfaces only:

```text
IRecallOrchestrator
IMemoryReflectionService
IMemoryContextPackRenderer
```

`WorkspaceMemoryContextProvider` should remain a compatibility fallback. It should not become the cognitive memory extension point.

## RAG Integration Boundary

Keep RAG driver provider-neutral. Cognitive Memory should add a projection manager above it.

```text
CognitiveMemory Projection Engine
        |
        v
IRagDriver / IRagDriverFactory
        |
        v
QdrantRagDriver
```

Do not put Cognitive Memory semantics into the generic RAG driver. Add generic filter/named-vector features to RAG, then use them from Cognitive Memory.

## Semantic Driver Boundary

The user's custom semantic driver should be exposed as a neutral embedding service:

```csharp
public interface ISemanticEmbeddingService
{
    ValueTask<SemanticEmbeddingResult> EmbedAsync(
        SemanticEmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
```

Adapters:

- SemanticCompletion ONNX adapter
- Ollama adapter
- OpenAI/API adapter if available
- test deterministic hashing adapter

## Plugin Boundary

Plugins can contribute sources but should not write canonical memory directly.

```text
Plugin source/executor
  -> emits source item / evidence / event
  -> Cognitive Memory source scanner canonicalizes later
```

## Process Boundary

Processes already store decisions, artifacts, journal entries, conformance observations, improvements. Cognitive Memory should read them through a process source adapter.

Future optional integration:

- publish a process event after step completion,
- enqueue reflection/consolidation job,
- create episode record.

## Storage Boundary

Use storage/IPFS for large immutable artifacts:

- raw source snapshots,
- generated context packs,
- consolidation reports,
- canonicalization evidence bundles,
- review packages.

Relational DB stores metadata and references.

## UI Boundary

Cognitive Memory UI should be a module page and shared components:

- Memory Explorer
- Source Manifest page
- Recall Trace viewer
- Consolidation Queue page
- Human Review page
- Mindmap projection inspector
- Qdrant projection health page

Use BaseLib/CanvasLib/WebGLLib where appropriate, but do not block the backend architecture on UI polish.

## Interactive Probing Boundary

Add a probing subdomain inside Cognitive Memory rather than inside MAF:

```text
CanDoItAll.CognitiveMemory.Core
  -> probe orchestration, assessment, calibration, regression-test logic
CanDoItAll.Modules.CognitiveMemory
  -> EF records, repositories, review integration, evidence publication
CanDoItAll.Modules.CognitiveMemory.Components
  -> Dialogue Workbench UI
CanDoItAll.CognitiveMemory.Maf
  -> optional workflow/tool wrappers only
```

MAF may execute a probe workflow or question-generation agent, but durable probe sessions, feedback records, corrections, regression tests, and evidence publication remain Cognitive Memory state.

## Existing Source Snapshot Adapter Rule

The uploaded current code already provides source snapshot contracts in `CanDoItAll.AgentFramework.Core`. Cognitive Memory should define an adapter layer from these snapshot DTOs into durable memory source records. Do not implement parallel Workbench/Process/Workflow readers inside the Cognitive Memory module.
