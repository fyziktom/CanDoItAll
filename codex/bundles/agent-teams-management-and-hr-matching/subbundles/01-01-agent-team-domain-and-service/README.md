# 01-agent-team-domain-and-service

## Status

- `Completed`

## Objective

- Add durable AgentFramework team records and service operations that support many-to-many agent membership.

## Covered Inputs

- `N001`: Agents teams, management, and usage.
- `N002`: Teams have multiple agents.
- `N003`: Team creation belongs in the Agents module.
- `N008`: An agent can belong to multiple teams.

## Prerequisites

- Bundle prepared-stage validator has passed or any failures are repaired.
- Current AgentFramework catalog service and workspace model files are rechecked before edits.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workspace\WorkspaceModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\AgentModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Contracts\Contracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Validation\SandboxWorkspaceDocumentInvariantValidator.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\AgentsApi.cs

## Deliverables

- `AgentTeamDefinition` or equivalent catalog model.
- Workspace catalog/document team preservation through load/save/combine operations.
- Workspace service methods for listing, saving, deleting, and updating team members.
- API endpoints for agent team operations.
- Tests proving persistence, many-to-many membership, and agent deletion membership pruning.

## Dependency Impact

- Agents tab tree and membership modal depend on team list and member ids.
- Process HR matching depends on resolving team technical agent ids.
- Weak proof here invalidates all UI and process matching proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add team model and catalog/document properties with safe defaults for existing JSON.
2. Update catalog normalization and invariant validation.
3. Add service contract and implementation methods.
4. Update delete/prune behavior for agent removal.
5. Add HTTP API endpoints.
6. Add targeted tests.

## Scope Exceptions

- Do not create CRM-HR team parties or process database tables in this subbundle.

## Do Not Do

- Do not change provider or secret ownership.
- Do not alter chat, execution, or process runtime behavior.
- Do not use tags as the only team source of truth.

## Acceptance Checklist

- [x] Teams persist after save/load.
- [x] One team can hold multiple agent ids.
- [x] One agent id can appear in multiple teams.
- [x] Deleting an agent removes that id from every team.
- [x] Existing agent list/save/delete tests still pass through targeted build and team integration coverage.

## Proof Required

- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore` succeeded.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~AgentTeamCatalogIntegrationTests"` passed: 2 tests.

## Browser Validation Logging

- `N/A` for this foundation subbundle because it does not directly change browser-visible markup.

## Progression Gate

- Continue to subbundles 02 and 03 only after team service tests pass and the execution report records the command result.

## Suggested Agent Prompt

```text
Implement only the AgentFramework team domain and service foundation. Preserve existing catalog behavior, add durable many-to-many team membership, prove save/load and deletion pruning with targeted tests, update the execution report, and stop if catalog normalization cannot safely preserve teams.
```
