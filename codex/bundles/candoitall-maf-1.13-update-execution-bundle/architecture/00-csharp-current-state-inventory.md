# C# Current State Inventory

## Source Files Inspected

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowCompiler.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafInProcessWorkflowExecutionBackend.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowEventNormalizer.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowLlmComponentInvoker.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`

## CodeAnalytics Evidence

- Snapshot id: `snap-20260707234748-ac72a0ea`.
- Snapshot scope: `CanDoItAll.AgentFramework`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Processes`, `CanDoItAll.Processes`.
- Snapshot health: 94 projects, 2146 documents, no blocking errors.
- Project inventory confirmed `CanDoItAll.AgentFramework.Maf` directly references MAF stable packages, A2A, Mem0, OpenAI, Workflows, ModelContextProtocol, OllamaSharp, and OpenTelemetry.
- Project inventory confirmed `CanDoItAll.AgentFramework.Workflows.MafAdapter` directly references MAF stable packages plus `Microsoft.Extensions.AI.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`.
- Dependency query reported module/type cycles in `CanDoItAll.Modules.AgentFramework` node ids. This is not automatically in scope, but changed dependencies must be checked in `SB04`.

## Large Classes And Partial Classes

| Type/file | Shape | Policy |
|---|---|---|
| `MafAgentRuntime` | 1470-line runtime facade implementing `IAgentRuntime`; public constructor has 3 input parameters and resolves multiple collaborators. | Do not split as part of the package update. If compile fixes require moving behavior, create a minimal adapter/top-level type with direct tests and source assertions. |
| `RuntimeCapabilityComposer` | 1123-line composer plus related partial files; constructor has 8 parameters and creates descriptor/access/tool-provider helpers. | No new partial files as final architecture. Package compatibility changes must stay in existing capability adapters or focused top-level helpers. |
| `MafRuntimeAgentFactory` | 683-line factory with 6 constructor parameters. | Keep construction behavior cohesive; avoid service-location shortcuts. |
| `MafFinalizerDriver` | 927 lines, already separate from runtime. | Preserve finalizer behavior; do not replace with free-form JSON parsing. |
| `McpCapabilityBuilder` | 947 lines. | Touch only if MAF package compile errors require it. |

## Constructor Dependency Counts

| Type | Constructor shape | Count |
|---|---|---:|
| `MafAgentRuntime` | `workspaceRoot`, `services`, optional `workspaceScope`; resolves runtime collaborators internally. | 3 explicit parameters |
| `RuntimeCapabilityComposer` | `workspaceRoot`, `services`, `workspaceScope`, `dependencyResolver`, `providerCredentialService`, `providerRuntimeGateway`, `runtimeToolProviderComposer`, `compositionMetrics`. | 8 parameters |
| `MafRuntimeAgentFactory` | `workspaceRoot`, `services`, `workspaceScope`, `providerCredentialService`, `providerAgentFactory`, `runtimeCapabilityComposer`. | 6 parameters |
| `MafProviderStreamingRunner` | primary constructor with `IMafProviderStreamingDispatchGate`. | 1 parameter |
| `MafWorkflowCompiler` | primary constructor with validator plus optional executor, LLM, and routing compilers. | 4 parameters |
| `MafInProcessWorkflowExecutionBackend` | two overloads: compiler plus components or component library, with optional event normalizer/checkpoint/payload services. | 5 parameters per overload |

## Direct Instantiation Points

- `MafAgentRuntime` constructs default runtime collaborators when services do not provide alternatives.
- `RuntimeCapabilityComposer` constructs descriptor/access/planner/attacher collaborators.
- `MafWorkflowCompiler` constructs default routing compiler when not supplied.
- `MafInProcessWorkflowExecutionBackend` constructs default event normalizer, checkpoint factory, and payload policy service when not supplied.

## Provider Tool Driver Memory Responsibilities

- Provider selection and gateway remain under `CanDoItAll.AgentFramework.Providers` and MAF runtime gateway abstractions.
- Runtime tool providers enter through `IAgentRuntimeToolProvider` and `RuntimeToolProviderComposer`; do not add process-specific runtime tools in this phase.
- Workflow adapter maps CanDoItAll workflow abstractions to MAF workflows; process runtime remains product/runtime flow, not default MAF workflow adoption.
- Mem0 package compatibility is direct package-surface risk only. Memory provider abstractions are not changed in phase 1 unless restore/build proves unavoidable.

## Current Tests

- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafPackageBaselineReflectionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ProviderDispatchLaneGateTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ProviderRuntimeLifecycleTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafWorkflowAdapterIsolationTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafWorkflowEventNormalizerTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`

## Missing Tests

- Direct adapter tests for any new compatibility shim introduced for MAF API changes.
- Negative tests proving missing finalizer, invalid approval continuation, unsupported provider/session state, or unsafe tool mutation is not silently accepted.
- Composition smoke proving any new package-floor or preview-package decision wires correctly.
- Source scan proof that no new direct process runtime provider was added.

## Risk Notes

- The largest risk is weakening governance behavior to satisfy new package APIs.
- The second-largest risk is broadening the update to unrelated packages because NuGet reports newer versions.
- The architecture gate should reject any fix that makes future MAF/package extensions require another runtime partial class.
