# C# Current State Inventory

## Source Files Inspected

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeServiceCollectionExtensions.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.CatalogDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolDescriptors.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.RuntimeToolProviders.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/AgentFrameworkWorkspaceFactory.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`

## Large Classes And Partial Classes

| Type | Current state |
| --- | --- |
| `MafAgentRuntime` | Top-level non-partial class, but still large and responsibility-heavy. |
| `RuntimeCapabilityComposer` | Top-level partial class split across access, policy, descriptor, runtime-tool provider, and main composer files. This is not an acceptable final architecture boundary. |
| `MafRuntimeAgentFactory` | Top-level class, but mixes runtime build, handoff, policy, instrumentation, finalizer capture, credential environment, and chat-history behavior. |
| `WorkspaceRuntimePlugin` | Top-level class, but mixes several tool families and policy/path behavior. |
| `ToolCapabilityBuilder` | Partial class. Named as downstream risk; only in scope if needed to complete the capability-composer split. |

## Constructor Dependency Counts

| Type | Constructor inputs or held dependencies | Architecture note |
| --- | --- | --- |
| `MafAgentRuntime` | `workspaceRoot`, `IServiceProvider`, optional `WorkspaceScopeDescriptor`; internally resolves or constructs dependency resolver, credential service, provider factory, provider gateway, streaming runner, runtime composer, runtime factory, input preparer. | Hidden dependencies remain; runtime constructor does composition work. |
| `RuntimeCapabilityComposer` | `workspaceRoot`, `IServiceProvider`, workspace scope, dependency resolver, credential service, provider gateway, runtime tool provider composer, metrics. | Broad service access and broad construction surface. |
| `MafRuntimeAgentFactory` | `workspaceRoot`, `IServiceProvider`, workspace scope, credential service, provider agent factory, capability composer. | `IServiceProvider` remains in runtime build path and is passed to provider agent factory. |
| `WorkspaceRuntimePlugin` | workspace file service, command service, artifact service, workspace root/scope, access settings, provider, runtime model, provider gateway. | Tool families and provider image-analysis dependency are coupled. |

## Direct Instantiation Points

- `MafAgentRuntime` constructs `RuntimeCapabilityComposer` and `MafRuntimeAgentFactory`.
- `AgentFrameworkServiceCollectionExtensions` registers `new MafAgentRuntime(...)`.
- `AgentFrameworkModuleServiceCollectionExtensions` registers `new MafAgentRuntime(...)`.
- `CanDoItAllAgentWorkspaceFactory` creates `new MafAgentRuntime(...)` for alternate workspace scopes.
- `RuntimeCapabilityComposer` constructs `WorkspaceRuntimePlugin`.

## Current Tests

- `MafRuntimeArchitectureServicesTests` verifies some top-level collaborators and guards against `partial class MafAgentRuntime`.
- `MafAgentRuntimeToolProviderCompositionTests` exercises composer/tool behavior but often through `RuntimeCapabilityComposer.CreateDefault(...)`.
- `MafAgentRuntimeProviderHealthTests` constructs `MafAgentRuntime` directly for provider diagnostics.
- `MafAgentRuntimeHandoffTests` provides focused integration smoke.

## Missing Tests

- No guard blocks `partial class RuntimeCapabilityComposer` as a final boundary.
- No unit tests instantiate a `MafRuntimeTurnCoordinator` or equivalent because it does not exist.
- No unit tests isolate runtime session persistence from `MafAgentRuntime`.
- No unit tests isolate approval continuation mapping/cache behavior from `MafAgentRuntime`.
- No unit tests isolate finalizer repair orchestration from the full runtime.
- No extension seam test proves a new workspace tool family or capability provider can be added without editing old large types.

## Risk Notes

- The existing broad classes have many provider/tool/model interactions, so each extraction needs characterization tests before moving logic.
- `IServiceProvider` cannot be removed blindly; keep it at composition boundaries, but extract explicit dependencies into the collaborators that own behavior.
- `Microsoft.Agents.AI` types may be difficult to fake. If needed, introduce narrow adapters around `AIAgent`, `AgentSession`, and streaming updates, but do not wrap the whole SDK in a generic abstraction.
