# Scope Inventory

## In Scope

- AgentFramework model and service additions for team definitions and memberships.
- Agents tab team tree, team creation/editing/deletion, and membership modal.
- Process launch HR matching selected-team input and out-of-team candidate markers.
- Targeted tests and browser proof.

## Out Of Scope

- CRM-HR delivery-unit modeling for teams.
- Provider, secret, runtime execution, or workflow engine redesign.
- Full migration of historical process launch plans.
- Replacing the existing agent details dialog.

## Likely Test Areas

- `tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs`
- `tests\CanDoItAll.Tests.Components\AgentChatModalTests.cs`
- `tests\CanDoItAll.Tests.Integration\ProcessLaunchPlanningIntegrationTests.cs`
