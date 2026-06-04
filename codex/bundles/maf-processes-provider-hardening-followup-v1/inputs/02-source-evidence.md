# Source Evidence Map

Use these exact source references as the review baseline for this follow-up bundle:

| Evidence | Path | Notes |
| --- | --- | --- |
| Solution includes Tooling project | `repo://CanDoItAll.slnx` | New project is present in `/src/` folder. |
| MAF project references Tooling and no longer references Processes | `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | Still references Projects/Security/Workbench/Workspace. |
| Tooling project is neutral | `repo://src/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` | Only Models + Microsoft.Extensions.AI.Abstractions. |
| Runtime provider context | `repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs` | Contains Agent, Provider, Capabilities, suppress approval flag, Purpose, RuntimeSessionKey, Tags. |
| Runtime provider interface | `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs` | Returns `IReadOnlyList<AITool>`. |
| Runtime provider purpose enum | `repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderPurpose.cs` | Includes InteractiveChat, GovernedProcessAutomation, AutoApprovedNonInteractive, A2AEndpoint. |
| MAF provider composition | `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs` | Attaches registered providers and still contains project/image attach code. |
| Process provider registration | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Registers `ProcessAgentRuntimeToolProvider`. |
| Process tool provider | `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs` | Large provider containing process tools/access guards/DTOs. |
| Previous execution report | `repo://codex/bundles/maf-processes-decoupling-bundle-v1/reviews/01-execution-report.md` | SB01-SB09 reported pass. |
| Previous red-team review | `repo://codex/bundles/maf-processes-decoupling-bundle-v1/reviews/02-final-red-team-review.md` | Scoped decoupling passed; core extraction and driver packs remain future work. |
