# Scope Inventory

## Runtime Files

| Surface | Source | Current observation |
| --- | --- | --- |
| Main runtime | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | 2185 lines; public runtime path plus fallback provider gates, approvals, response/finalizer recovery, helper logic. |
| Agent/provider factory | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | 1709 lines; provider client construction, credential resolution, runtime build result, hosted wrapper, nested `RuntimeCapabilityState`. |
| Capability lifecycle | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | 963 lines; capability-state lifecycle and composition root behavior. |
| Runtime provider attachment | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.RuntimeToolProviders.cs` | 451 lines; provider enumeration/materialization/filtering/metadata/approval wrapping. |
| Built-in tools | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs` | 404 lines; built-in tool mapping and plugin tool behavior inside nested builder. |
| MCP behavior | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | 822 lines; hosted/local/remote MCP behavior inside nested builder. |
| Context behavior | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs` | 275 lines; RAG/static/Mem0 context behavior inside nested builder. |
| Skill behavior | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs` | 220 lines; skill attachment and file skill policy behavior inside nested builder. |
| Workspace tools | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` | 924 lines; workspace tools are hidden as nested runtime plugin. |
| Storage tools | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.StorageRuntimePlugin.cs` | 236 lines; storage plugin nested under runtime. |
| Finalizer driver | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafFinalizerDriver.cs` | 804 lines; already partly separated but still part of runtime closure strategy. |
| Session builder | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs` | Existing candidate for session factory boundary. |
| Provider gateway | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs` | Existing provider runtime gateway candidate. |

## Extension Points And Models

| Surface | Source | Current observation |
| --- | --- | --- |
| Runtime tool provider interface | `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs` | Existing extension point; composition still happens inside MAF partial. |
| Provider descriptor | `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderDescriptor.cs` | Descriptor metadata can support prefiltering and test assertions. |
| Provider context | `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs` | Existing context model for provider tool creation. |
| Access models | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Agents/Access` | Existing typed access models should feed extracted access planner/composer. |
| Tool policy registry | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs` | Existing metadata registry used by runtime provider filtering and operation policy. |

## Test Surfaces

| Test source | Current observation |
| --- | --- |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs` | Strong coverage, but tests depend on full runtime construction and private reflection helpers. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs` | Context contributor tests reach private runtime methods through reflection. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs` | Finalizer policy tests reach nested/private runtime types. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs` | Attachment tests reach nested/private analysis types and helpers. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs` | Provider health tests instantiate runtime directly. |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ToolImplementationContractsTests.cs` | Existing typed tool contract tests can inspire runtime contract tests. |
| `repo://tests/Integration/CanDoItAll.Tests.Integration/MafAgentRuntimeHandoffTests.cs` | Integration coverage that must remain green after extraction. |

## Missing Inventory Items To Complete In SB01

- Exact responsibility map by method group and extracted target owner.
- Current runtime construction paths used by app hosts and tests.
- Baseline command set for MAF runtime unit/integration tests.
- Local timing baseline for capability composition and provider attachment.
- Reflection-dependent test list that should shrink after extraction.
