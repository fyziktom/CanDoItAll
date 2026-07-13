# Current State

## Preparation Evidence

- Working tree state during preparation: `git status --short` returned no output.
- Current branch during preparation: `memory-providers`.
- `Directory.Packages.props` was not present at repository root.
- `CanDoItAll.slnx` exists at repository root.
- No `packages.lock.json` files were found by recursive search.
- CodeAnalytics snapshot id: `snap-20260707234748-ac72a0ea`.
- Snapshot scope: `CanDoItAll.AgentFramework`, `CanDoItAll.Modules.AgentFramework`, `CanDoItAll.Modules.Processes`, `CanDoItAll.Processes`.
- Snapshot health: 94 source projects, 2146 documents, no blocking errors.
- Snapshot diagnostics included existing MSBuild/package warnings for `Microsoft.OpenApi` 2.0.0 vulnerability in app/test/tool projects. Treat as pre-existing unless package-update implementation changes it.

## Current Package References

| Project | Package | Current | Preparation decision |
|---|---:|---:|---|
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI` | `1.8.0` | Update to `1.13.0`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.A2A` | `1.8.0-preview.260528.1` | NuGet CLI showed `1.13.0-preview.260703.1`; update only after execution rechecks CLI. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.Mem0` | `1.0.0-preview.251028.1` | NuGet CLI reported not found from configured sources; do not guess. Keep or isolate only if restore/build proves a real issue. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.OpenAI` | `1.8.0` | Update to `1.13.0`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.Workflows` | `1.8.0` | Update to `1.13.0`. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Agents.AI` | `1.8.0` | Update to `1.13.0`. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Agents.AI.Workflows` | `1.8.0` | Update to `1.13.0`. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Extensions.AI.Abstractions` | `10.5.1` | Update to `10.6.0` if required by MAF 1.13 dependency floor; do not chase `10.7.0` unless restore/build requires it. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.7` | Update to `10.0.9` if required by MAF 1.13 dependency floor. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `Microsoft.Agents.AI.Hosting.A2A` | `1.8.0-preview.260528.1` | NuGet CLI showed `1.13.0-preview.260703.1`; include in preview decision gate. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.7` | Update only if restore/build produces a dependency-floor issue. |
| `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` | `Microsoft.Extensions.AI.Abstractions` | `10.5.1` | Update only if restore/build produces a dependency-floor issue. |

## Available Package Evidence

Read-only `dotnet list package --outdated --include-prerelease` observed:

- `Microsoft.Agents.AI`: latest `1.13.0`.
- `Microsoft.Agents.AI.OpenAI`: latest `1.13.0`.
- `Microsoft.Agents.AI.Workflows`: latest `1.13.0`.
- `Microsoft.Agents.AI.A2A`: latest `1.13.0-preview.260703.1`.
- `Microsoft.Agents.AI.Hosting.A2A`: latest `1.13.0-preview.260703.1`.
- `Microsoft.Agents.AI.Mem0`: not found in configured sources.
- `Microsoft.Extensions.AI.Abstractions`: latest `10.7.0` for tooling; this bundle still treats `10.6.0` as the conservative MAF floor target unless restore/build proves otherwise.
- `Microsoft.Extensions.DependencyInjection.Abstractions`: preview `11.0.0-preview.5.26302.115` available for hosting; do not adopt preview 11 in this update.

## Source Hotspots

| File | Observed shape | Package-update risk |
|---|---|---|
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | 1470 lines; implements `IAgentRuntime`; public constructor has 3 parameters and resolves many runtime collaborators. | Streaming, approvals, finalizer repair, context manifests, usage observations, session state. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs` | 1123 lines plus related partials; constructor has 8 parameters and builds descriptor/access/tool-provider helpers. | Skills, context, compaction, workspace tools, A2A, MCP, registered runtime providers. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs` | 683 lines; constructor has 6 parameters. | `AIAgent.AsBuilder`, options, middleware, approval wrapper, logging, OTel. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs` | Static session/run-options builder. | `AgentSession`, continuation token, response format and history behavior. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs` | Primary constructor has 1 gate dependency. | `AIAgent.RunStreamingAsync`, `AgentResponseUpdate`, content snapshot types. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowCompiler.cs` | Primary constructor has validator plus optional executor, LLM, and routing compilers. | Workflow builder/API changes. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafInProcessWorkflowExecutionBackend.cs` | Two public constructors; non-durable in-process backend. | Workflow start/checkpoint/event surface changes. |

## Test Targets Found

- `tests/Unit/CanDoItAll.Tests.Unit/MafPackageBaselineReflectionTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ProviderDispatchLaneGateTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/Providers/ProviderRuntimeLifecycleTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/MafWorkflowAdapterIsolationTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/MafWorkflowEventNormalizerTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`

## Current Ambiguity To Preserve

Source scans still find legacy/planned direct `processes_*` policy/test names and docs. The current source tree does not contain a concrete `ProcessAgentRuntimeToolProvider`; this package update must not reintroduce one accidentally.
