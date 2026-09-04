# Current state and coupling analysis

## AgentsHomePage

Current responsibilities:

- route/query parameter ingestion and compatibility parsing;
- selected top-level tab, agent, team, Simple Chat state, and usage scope;
- page-header statistics and dashboard rendering;
- overview and usage aggregation;
- direct EF query of `AiResourceBinding` for bound-resource count;
- HR managed-agent resolution and avatar map construction;
- catalog warmup/default-feed confirmation;
- global navigation to CRM/HR, workflows, and processes;
- usage-detail dialog orchestration;
- managed HR chat launch;
- agent-chat context publication.

Current injected dependencies:

1. `NavigationManager`;
2. `IAgentFrameworkWorkspaceService`;
3. `ProviderUsageQueryService`;
4. `IAgentChatLauncher`;
5. `AgentFrameworkCatalogWarmupService`;
6. `IDbContextFactory<AppDbContext>`;
7. `NotificationService`;
8. `DialogService`.

Problem: the route-owning page legitimately owns navigation and host actions, but also
owns cross-source data orchestration and persistence access. State is represented by a
mixture of strings, nullable IDs, nested state, flags, and child callbacks rather than one
typed workspace model.

## AgentCatalogPanel

Current responsibilities:

- loading agents, teams, and providers;
- optional catalog repair;
- deriving private-provider status;
- local search and tree expansion;
- selected agent/team state and requested-state reconciliation;
- child-private suppression of repeated requested-agent dialog opening;
- create/edit/delete agent flows;
- create/edit/members/delete team flows;
- managed-agent quick-chat launch;
- global notifications and dialogs;
- catalog reload after mutations.

Current injected dependencies:

1. `IAgentFrameworkWorkspaceService`;
2. `IProviderRuntimeAdministrationService`;
3. `IAgentFrameworkOrganizationCatalogRepairService`;
4. `NotificationService`;
5. `DialogService`;
6. `IAgentChatLauncher`.

Problem: the component is simultaneously data source, controller, view, dialog host, and
route-echo guard. Parent and child both own parts of selection/detail state.

## AgentDetailsDialog

Current responsibilities:

- loading agent definition/editor, agents, providers, capabilities, secrets, and projects;
- ten-section editor navigation using a numeric index;
- draft model mutation and validation;
- provider/model/thinking-effort selection;
- image and avatar integration;
- project, workspace, storage, secret, process, capability, and voice access editing;
- lazy project list, partial provider/secret failures, capability refresh/verification;
- save canonicalization and persistence;
- delete confirmation and persistence;
- capability setup dialog and immediate persistence for existing agents;
- notifications and dialog completion semantics.

Current injected dependencies:

1. `IAgentFrameworkWorkspaceService`;
2. `IProviderRuntimeAdministrationService`;
3. `ProjectsService`;
4. `SecretService`;
5. `NotificationService`;
6. `DialogService`;
7. `IExternalTargetPathRegistryFactory`.

Problem: a substantial feature editor exposes no explicit load/session boundary and must
be test-seeded by mutating private fields. Concrete cross-module and infrastructure
services prevent a lightweight sandbox contract.

## Assembly-level limitation

All three components remain in the current broad AgentFramework Razor project. Therefore
this bundle improves logical boundaries and testability but does not yet shrink the
project-reference graph watched when that assembly changes. Physical extraction and a
small sandbox host are downstream work unlocked by this bundle, not claimed benefits of
this implementation alone.
