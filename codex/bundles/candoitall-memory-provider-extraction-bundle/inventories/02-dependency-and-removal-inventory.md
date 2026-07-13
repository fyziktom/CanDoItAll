# Dependency And Removal Inventory

## Direct references to remove or replace

| Current dependency | Source | Replacement |
| --- | --- | --- |
| Main composition references `CanDoItAll.Modules.CognitiveMemory` | `repo://src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj` | Reference `CanDoItAll.Modules.Memory`; native provider driver optional. |
| Module assembly discovery includes native memory assembly | `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs` | Include generic memory module only. |
| Base runtime calls `AddCognitiveMemoryModule` | `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | Call `AddMemoryModule` and optional configured providers. |
| Base runtime configures Qdrant for memory projection | `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | Move Qdrant to native provider/projection configuration. |
| Composition project references Qdrant and SemanticCompletion directly | `repo://src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj` | Remove from base composition; keep only optional provider/projection packages or native service packages. |
| Native memory registers MAF contributor/executors directly | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/CognitiveMemoryModuleServiceCollectionExtensions.cs` | Generic MAF memory package registers generic contributor/tool/executor. |
| Current MAF source snapshot contracts live under AgentFramework Core | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs` | Rehome or wrap through generic Source Gateway; avoid duplicate source snapshot contracts. |
| Native memory uses `AppDbContext` | `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory` | Native `CognitiveMemoryDbContext`; source data through Source Gateway snapshots. |
| Host API exposes native endpoints | `repo://src/App/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`, `repo://src/App/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`, `repo://src/App/CanDoItAll.Web/Api/CognitiveMemoryApi.RecallReviewEndpoints.cs` | Generic Memory endpoints and native service remote API. |

## Guard conditions

- `grep -R "CanDoItAll.Modules.CognitiveMemory" src/MAF src/Modules/CanDoItAll.Modules.Memory src/App/CanDoItAll.Composition` must be clean after SB30 except documented migration shims.
- `grep -R "Qdrant" src/App/CanDoItAll.Composition` must be clean after SB30 except unrelated non-memory comments if any.
- Generic memory contracts must not contain `CognitiveMemory` type names except compatibility mapping tests or documented migration adapters.
- Zero-provider startup audits must prove no calls reach native Cognitive Memory, OpenAI, Qdrant, or mock providers unless an explicit provider profile is configured.
- Source Gateway audits must prove there is one active source snapshot contract path or a documented adapter between old and new paths.
