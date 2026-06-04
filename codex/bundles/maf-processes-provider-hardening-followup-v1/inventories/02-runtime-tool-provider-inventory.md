# Runtime Tool Provider Inventory

## Existing Provider Contracts

| Contract | Path | Current role | Gap |
| --- | --- | --- | --- |
| `IAgentRuntimeToolProvider` | `src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs` | Creates raw AITools | Needs descriptor/metadata support. |
| `AgentRuntimeToolProviderContext` | `src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs` | Carries agent/provider/capabilities/purpose/tags | Purpose/tags not yet deeply used. |
| `AgentRuntimeToolProviderPurpose` | `src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderPurpose.cs` | Basic runtime purpose enum | Needs tests and provider policy mapping. |

## Existing First-Party Provider

| Provider | Path | Current status | Follow-up |
| --- | --- | --- | --- |
| `ProcessAgentRuntimeToolProvider` | `src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs` | Registered and working | Split + purpose hardening. |

## Candidate Providers To Extract

| Candidate | Current evidence | Target owner | Follow-up |
| --- | --- | --- | --- |
| Project-structure provider | `AttachInternalProjectStructureToolsAsync` in MAF | Workbench/ProjectStructure owner | SB04. |
| Image-generation provider | `AttachInternalImageGenerationToolsAsync` in MAF | AgentFramework module or image owner | SB05. |
