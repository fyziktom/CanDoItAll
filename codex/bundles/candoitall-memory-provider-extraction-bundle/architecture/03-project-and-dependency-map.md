# Project And Dependency Map

## Main repository dependency direction

```text
CanDoItAll.Web
  -> CanDoItAll.Composition
  -> CanDoItAll.Modules.Memory
  -> CanDoItAll.Memory.Application
  -> CanDoItAll.Memory.Abstractions
```

MAF integration must point inward to generic contracts only:

```text
CanDoItAll.AgentFramework.Memory
  -> CanDoItAll.Memory.Abstractions
  -> CanDoItAll.Memory.Application abstractions
  -> current MAF abstractions only
```

Current MAF abstraction references for implementation:

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `repo://src/MAF/WorkflowExecutors/CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/WorkflowExecutorContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`

Provider drivers depend on generic abstractions and optional transport libraries only:

```text
CanDoItAll.Memory.Drivers.Http -> Memory.Abstractions
CanDoItAll.Memory.Drivers.Mcp  -> Memory.Abstractions
CanDoItAll.Memory.Drivers.Mock -> Memory.Abstractions
```

The main app must not have this after final closure:

```text
MAF -> Native Cognitive Memory
CanDoItAll.Composition -> CanDoItAll.Modules.CognitiveMemory
Base startup -> Qdrant because memory is enabled
Generic Memory -> Native Cognitive Memory domain/persistence/UI
```

## Native service dependency direction

```text
CognitiveMemory.Service
  -> CognitiveMemory.Application
  -> CognitiveMemory.Domain
  -> CognitiveMemory.Persistence
  -> CognitiveMemory.Contracts
```

Optional native adapters:

```text
CognitiveMemory.Projection.Rag -> CognitiveMemory.Application + RAG/Qdrant driver
CognitiveMemory.Maf            -> CognitiveMemory.Application + MAF abstractions
CognitiveMemory.UI             -> CognitiveMemory.Contracts or API client
```

## Transition adapter

During migration, an in-process adapter can wrap the existing module behind the generic provider interface. This adapter is temporary and must be removed or replaced by the remote/native driver after SB27-SB31. It exists to reduce migration risk, not as the final architecture.

The adapter must be selected only through an explicit provider profile. It must not be registered as an implicit fallback when no provider is configured.
