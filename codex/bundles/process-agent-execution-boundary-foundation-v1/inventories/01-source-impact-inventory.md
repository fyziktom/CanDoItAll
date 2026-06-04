# Source Impact Inventory

| Area | Current source | Expected action |
| --- | --- | --- |
| MAF project references | `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | Preserve no Processes/Projects/Workbench references |
| Tooling contracts | `src/CanDoItAll.AgentFramework.Tooling/*` | Keep product-neutral |
| Runtime provider composition | `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities*.cs` | Preserve provider pipeline |
| Process provider | `src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider*.cs` | Preserve 23 process tools |
| Workbench provider | `src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` | Preserve 28 project-structure tools |
| Image provider | `src/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs` | Preserve image tool |
| Process dispatcher | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService*.cs` | Isolate direct AgentFramework execution calls |
| Process module registration | `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Register execution client/fallbacks safely |
