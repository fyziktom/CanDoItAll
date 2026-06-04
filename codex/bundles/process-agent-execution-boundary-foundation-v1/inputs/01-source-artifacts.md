# Source Artifacts

This validator-compatible file mirrors `inputs/02-source-artifacts.md`. Keep both files aligned when source references change.

## Current Provider Boundary

- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderDescriptor.cs`
- `repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolMetadata.cs`
- `repo://src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolOperationKind.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`

## Product Runtime Providers

- `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.Policy.cs`
- `repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.Workbench/Services/WorkbenchModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`

## Process Dispatcher Coupling

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Execution.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.*.cs`

## Previous Bundle Proof

- `repo://codex/bundles/maf-processes-provider-hardening-followup-v1/reviews/01-execution-report.md`
- `repo://codex/bundles/maf-processes-provider-hardening-followup-v1/proof/SB12/manifest.md`
- `repo://codex/bundles/maf-processes-provider-hardening-followup-v1/proof/SB12/source-assertions/next-phase-cutline.md`
- `repo://codex/bundles/maf-processes-provider-hardening-followup-v1/proof/SB12/transcripts/final-hidden-dependency-and-scope-scan.txt`
- `repo://codex/bundles/maf-processes-provider-hardening-followup-v1/proof/SB12/transcripts/final-dotnet-build-slnx.txt`
