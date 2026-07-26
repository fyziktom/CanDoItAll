# Scope Inventory

## CanDoItAll

- HTTP boundary: `src/App/CanDoItAll.Web/Api/AgentsApi.cs`,
  `WorkflowsApi.cs`, `CrmHrApi.cs`, `ApiEndpointResults.cs`,
  `ApiServiceCollectionExtensions.cs`, `Program.cs`.
- Agent contracts/state: `AgentFramework.Models` editor, catalog, conversation, output,
  workflow, and sandbox workspace models.
- Agent application boundary: `AgentFramework.Core/Contracts/Contracts.cs`,
  catalog and execution services, workspace facade.
- Agent package implementation:
  `AgentFramework.Persistence/Packages/ZipAgentPackageService.cs`.
- Workflow boundary: `Workflows.Abstractions/WorkflowServiceContracts.cs`,
  `Workflows.Core/WorkflowCatalogServices.cs`, `WorkflowLaunchService.cs`, and module
  persistent workflow stores.
- Recruiting adjacency: `Modules.CrmHr` recruiting models/services/persistence. Existing
  CRM-HR interview rows are application-centric and are not the new canonical agent
  evidence store.
- Validation: `CanDoItAll.Tests.Integration` agent/workflow/CRM-HR API hosts and tests plus
  focused unit tests for extracted services.

## SharedInfo

- `docs/standards/codex.md`
- `codex/skills/_candoitall-api-shared`
- `codex/skills/candoitall-api-agents`
- `codex/skills/candoitall-api-workflows`
- `codex/skills/candoitall-api-crmhr`
- a new agent-recruiting reference or discoverable skill only if the shipped route family
  is independently operable
- `tools/validation/Test-CanDoItAllWebOpenApi.ps1`
- `tools/validation/Test-SharedInfo.ps1`

## Explicitly Out Of Scope

- partner example pack changes outside the two authorized repositories;
- UI/component changes;
- unrelated dependency vulnerability remediation;
- direct database scripts or seed-only endpoints.
