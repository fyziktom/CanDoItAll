# Scope Inventory

## MAF Runtime Partial Files

| File | Lines | Phase |
| --- | ---: | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` | 2204 | SB06/SB08 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs` | 1160 | SB03/SB06 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | 1024 | SB02/SB03 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | 823 | SB04 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs` | 924 | SB05 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs` | 404 | SB04/SB05 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Access.CatalogDescriptors.cs` | 431 | SB03/SB07 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs` | 275 | SB04 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs` | 220 | SB04 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.InputAttachments.cs` | 184 | SB05 |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceSearchSupport.cs` | 190 | SB05 |
| Remaining `MafAgentRuntime*.cs` partials | 811 | SB02/SB03/SB07 |

## Private Nested Types To Remove Or Justify

| Type | Source | Planned Action |
| --- | --- | --- |
| `ContextCapabilityBuilder` | `Capabilities.Context.cs:13` | Extract top-level builder; remove `MafAgentRuntime owner`. |
| `WorkspaceMemoryContextProvider` | `Capabilities.Context.cs:238` | Extract top-level provider or move under context builder file. |
| `StaticMessageContextProvider` | `Capabilities.Context.cs:305` | Extract top-level provider. |
| `AgentRuntimeConfiguration` | `Capabilities.cs:983` | Extract top-level configuration model. |
| `FileSkillExecutionPolicy` | `Capabilities.cs:1000` | Extract top-level skill policy record. |
| `AgentRuntimeContextPolicyKind` | `Capabilities.cs:1005` | Extract top-level enum. |
| `RuntimeCompactionDecision` | `Capabilities.cs:1013` | Extract top-level decision record. |
| `RuntimeCapabilityComposition` | `Capabilities.cs:1033` | Extract top-level composition record without nested builder references. |
| `SkillCapabilityConfiguration` | `Capabilities.cs:1044` | Extract top-level configuration model. |
| `FileSkillScriptExecutionConfiguration` | `Capabilities.cs:1061` | Extract top-level configuration model. |
| `InlineSkillDefinition` | `Capabilities.cs:1068` | Extract top-level configuration model. |
| `InlineSkillResourceDefinition` | `Capabilities.cs:1079` | Extract top-level configuration model. |
| `McpCapabilityConfiguration` | `Capabilities.cs:1088` | Extract top-level configuration model. |
| `RagCapabilityConfiguration` | `Capabilities.cs:1123` | Extract top-level configuration model. |
| `AiContextCapabilityConfiguration` | `Capabilities.cs:1146` | Extract top-level configuration model. |
| `MemoryCapabilityConfiguration` | `Capabilities.cs:1153` | Extract top-level configuration model. |
| `PluginCapabilityConfiguration` | `Capabilities.cs:1176` | Extract top-level configuration model. |
| `BuiltInToolConfiguration` | `Capabilities.cs:1183` | Extract top-level configuration model. |
| `McpCapabilityBuilder` | `Capabilities.Mcp.cs:19` | Extract and split by MCP responsibility. |
| `BrowserMcpModelContextBoundedAIFunction` | `Capabilities.Mcp.cs:875` | Extract top-level tool wrapper. |
| `LocalMcpRuntimeAIFunction` | `Capabilities.Mcp.cs:902` | Extract top-level tool wrapper. |
| `LocalMcpRuntimeClientLease` | `Capabilities.Mcp.cs:909` | Extract top-level lease. |
| `WorkspaceRuntimePlugin` | `WorkspaceRuntimePlugin.cs:18` | Extract or split into workspace drivers/factory. |
| `SkillCapabilityBuilder` | `Capabilities.Skills.cs:8` | Extract top-level builder. |
| `ToolCapabilityBuilder` | `Capabilities.Tools.cs:11`, `Tools.ConfiguredWorkspace.cs:11` | Extract top-level builder and split contributors. |
| `PreparedInputAttachments` | `InputAttachments.cs:193` | Extract attachment result record. |
| `InputAttachmentAnalysis` | `InputAttachments.cs:198` | Extract attachment analysis record. |
| `RequiredFinalizerCapturedException` | `MafAgentRuntime.cs:2364` | Move to finalizer/recovery area or replace with typed result. |
| `RepeatedToolInvocationGuard` | `MafAgentRuntime.cs:2370` | Extract top-level guard service. |

## Known Existing Extracted Seams To Preserve

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolCapabilityDescriptorFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeDependencyResolver.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeExecutionContracts.cs`

## Tests To Repoint During Execution

- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeImageAnalysisModelTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeProviderHealthTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolInvocationResultTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafRuntimeArchitectureServicesTests.cs`
