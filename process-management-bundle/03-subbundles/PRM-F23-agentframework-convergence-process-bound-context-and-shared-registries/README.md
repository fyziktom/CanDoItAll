# PRM-F23 — AgentFramework convergence: process-bound context, rights, and shared registries

## Objective

Prepare the future AgentFramework bridge so external execution remains process-bound, rights-aware, and registry-convergent instead of creating a second permanent runtime world beside CanDoItAll.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 3**
- Depends on: **PRM-F13, PRM-F16, PRM-F22**

## Why this feature exists

Both uploaded repos already suggest convergence, but the bundle needed to make that convergence explicit before the future merge begins.

## In scope

- External executor correlation records for sessions, logs, approvals, and metrics
- Permission mapping from process governance to future external executor policy envelopes
- Explicit registry ownership rules for templates, providers, and capabilities
- Bridge contracts that remain compile-time independent from the current external repo

## Non-goals

- Do not move durable business identity ownership out of CRM-HR.
- Do not create a second permanent provider or capability registry in the process bridge.
- Do not take a compile-time dependency on the external AgentFramework repo in the first process-module merge.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/IProcessActorExecutionBridge.cs`
- `src/CanDoItAll.Modules.Processes/ProcessExecutionBridgeContracts.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessPermissionsMapping.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessExternalExecutionLinkModels.cs (new)`
- `src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs`
- `src/CanDoItAll.Modules.Workspace/WorkspaceModels.cs`
- `CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/AgentModels.cs (reference seam)`
- `CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Models/ConversationModels.cs (reference seam)`
- `CanDoItAll.AgentFramework-main/src/CanDoItAll.AgentFramework.Core/AgentFrameworkWorkspaceService.Chat.cs (reference seam)`
- `CanDoItAll.AgentFramework-main/integration-map/01-candoitall-seams.md (reference seam)`
- `CanDoItAll.AgentFramework-main/integration-map/02-data-rights-and-persistence.md (reference seam)`
- `tests/CanDoItAll.Tests.Unit/ProcessExecutionBridgeMappingTests.cs (new)`
