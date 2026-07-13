# Source Artifacts

## Repository Evidence

| Area | Source |
| --- | --- |
| MAF workspace image tools | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs` |
| MAF image-set deterministic evidence | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageSetEvidenceBuilder.cs` |
| MAF runtime context intent | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Runtime/AgentRuntimeContextAssemblyModels.cs` |
| MAF execution metadata | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/ExecutionInvocationMetadata.cs` |
| MAF execution run context creation | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` |
| MAF capability access planner | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.cs` |
| MAF process access policies | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.Access.Policies.cs` |
| MAF runtime tool-provider filtering | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeToolProviderComposer.cs` |
| Capability access abstractions | `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Abstractions/CapabilityModels.cs` |
| Capability access evaluator | `repo://src/MAF/Capabilities/CanDoItAll.AgentFramework.Capabilities.Access/CapabilityAccessPolicyEvaluator.cs` |
| Process allowed-operation compiler | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Capabilities/ProcessAllowedOperationsCapabilityPolicyCompiler.cs` |
| Process runtime assignment contract | `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs` |
| Process launch assignment builder | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` |
| Process template step document | `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs` |
| Process brief contracts | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs` |
| AgentFramework process prompt driver | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs` |
| Process-to-MAF metadata builder | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Metadata.cs` |
| MAF/process implementation map | `repo://docs/processes-maf-providers-implementation-map.md` |
| Unit policy tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentToolInvocationPolicyTests.cs` |
| Process prompt tests | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs` |
| Project-structure process integration tests | `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs` |

## CodeAnalytics Evidence

- Snapshot id: `snap-20260707120906-123d4de9`
- Solution: `repo://CanDoItAll.slnx`
- Scoped projects: MAF wrapper plus process abstractions, contracts, core, builder, application, runtime, driver abstractions, and standard drivers.
- Blocking errors: none reported.
- Notable hotspots: `MafAgentRuntime` at 1470 lines, `RuntimeCapabilityComposer.cs` at 1123 lines, `WorkspaceRuntimePlugin.cs` at 818 lines, and `ProcessRuntimeEngine` with broad source-member surface.
- Dependency cycles in scoped graph: none reported.

## Existing Bundle Context

This bundle follows the same initiative bundle style as `repo://codex/bundles/maf-runtime-architecture-isolation`.
