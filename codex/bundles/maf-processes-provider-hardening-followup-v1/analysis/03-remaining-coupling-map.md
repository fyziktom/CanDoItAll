# Remaining Coupling Map

## Already Closed

| Coupling | Status |
| --- | --- |
| `CanDoItAll.AgentFramework.Maf` direct project reference to `CanDoItAll.Modules.Processes` | Closed by previous branch. |
| Process tool builder implemented inside MAF | Closed by previous branch; process tools now live in `ProcessAgentRuntimeToolProvider`. |

## Still Present Or Needs Explicit Review

| Coupling | Evidence path | Bundle action |
| --- | --- | --- |
| MAF still references `CanDoItAll.Modules.Projects` | `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | Inventory/remove only if project-structure provider extraction makes it unused. |
| MAF still references `CanDoItAll.Modules.Workbench` | Same | SB04 extraction and SB06 allowed-list. |
| MAF still references `CanDoItAll.Modules.Workspace` | Same | Likely legitimate for workspace runtime tools; document allowed-list. |
| MAF still references `CanDoItAll.Modules.Security` | Same | Inventory usage and document/decouple later if feasible. |
| `AttachInternalProjectStructureToolsAsync` remains in MAF | `MafAgentRuntime.Capabilities.cs` | SB04 provider extraction. |
| `AttachInternalImageGenerationToolsAsync` remains in MAF | `MafAgentRuntime.Capabilities.cs` | SB05 provider extraction. |
| `ProcessAgentRuntimeToolProvider` combines too many responsibilities | `ProcessAgentRuntimeToolProvider.cs` | SB07 split. |
| Runtime provider context purpose is not yet strong policy | Tooling context + process provider | SB08 hardening. |
