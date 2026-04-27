# Repository Evidence Map

| Area | Current evidence | Notes |
|---|---|---|
| MAF packages | `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` references `Microsoft.Agents.AI` 1.0.0 and `Microsoft.Agents.AI.OpenAI` 1.0.0. | Native MAF usage exists. |
| MAF workflows package | `src/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj` references `Microsoft.Agents.AI.Workflows` 1.0.0. | Workflows are available, but current usage is mostly checkpoint store. |
| Structured contract model | `src/CanDoItAll.AgentFramework.Models/OutputContracts/AgentOutputContracts.cs` | Strong DTO foundation. |
| JSON validation pipeline | `src/CanDoItAll.AgentFramework.Core/OutputContracts/AgentOutputValidation.cs` | Strict JSON options and raw hash exist. Validators need expansion. |
| MAF response format | `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs:247-261` | Uses `ChatResponseFormat.ForJsonSchema(...)`. |
| Process structured outcome | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.OutputValidation.cs` | `ProcessStepOutcomeResult` is validated, but minimally. |
| Process execution uses structured output | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs:128` | Good initial-run behavior. |
| Approval continuation drops structured output | `src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:142` and `:160` | Concrete bug/risk. |
| MAF agent builder middleware | `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs:276-311` | Instrumentation exists; policy middleware missing. |
| Function tools | `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs` | Uses `AIFunctionFactory.Create(...)`. |
| Tool approval wrappers | `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`, `.Mcp.cs`, `.Capabilities.cs` | Approval wrappers exist. Need central policy. |
| Built-in enabled ignored | `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs:214-215` | Must honor config. |
| MCP validation | `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs` | Strong local MCP validation and secret-binding logic. |
| Session restore and structured run | `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.Session.cs` | Session serialization exists. Context boundaries need review. |
| Approval checkpoint bridge | `src/CanDoItAll.AgentFramework.Core/Execution/ExecutionCheckpointServices.cs` | Uses MAF checkpoint store for pending approvals only. |
| Compaction | `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs:305-398` | Useful but should be provider/session gated. |
| Repeated tool guard | `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs:563-665` | Useful concept, but currently post-stream and includes calculator-specific hints. |
| Provider structured-output flag | `src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs:139` | Needs central capability profile. |
| Managed provider default | `src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs:407` and `:574-576` | Forces structured-output false for managed SQLite provider. |
