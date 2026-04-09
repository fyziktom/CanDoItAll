# Codex task — PRM-F23

Implement **AgentFramework convergence: process-bound context, rights, and shared registries** inside the uploaded CanDoItAll solution.

## Constraints

- Treat CRM-HR as the canonical owner of durable role and agent identities.
- Treat Workspace as the canonical owner of shared provider profiles.
- Do not create a second permanent capability or provider registry in the bridge layer.
- Do not add direct compile-time dependency on the uploaded AgentFramework repo in the first merge.
- Keep all code comments in English.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- Future external executor correlations can link ProcessRun, ProcessStepRun, and assignment records to runtime session, log, and metric identifiers.
- CRM-HR remains canonical for business role and agent templates plus durable AI identities even if runtime-level templates exist elsewhere.
- Shared provider and capability ownership is explicitly converged so the process bridge does not introduce a second canonical registry.
- Process step governance can narrow or require approvals for future AgentFramework permissions and external-call behavior.

## Recommended first files to touch

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
