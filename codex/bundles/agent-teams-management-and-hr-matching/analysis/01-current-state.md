# Current State

## Platform Shape

- Repository targets .NET 10.0 through `global.json` and project target frameworks.
- `/agents` is a Blazor page in `CanDoItAll.Modules.AgentFramework`; the Agents tab renders `AgentCatalogPanel`.
- Agent data is owned by AgentFramework workspace catalog files through `IAgentFrameworkWorkspaceService`, `AgentFrameworkWorkspaceCatalogService`, and `FileSandboxWorkspaceStore`.
- CRM-HR projects AgentFramework agents into an AI resource directory through `AiAgentService` and the AgentFramework-to-CRM-HR bridge. Teams should not be modeled as CRM-HR parties unless a future requirement asks for organization-unit semantics.

## Agent Catalog Surface

- `AgentCatalogPanel` currently loads agents with `WorkspaceService.ListAgentsAsync(includeTemplates: false)` and filters only by free-text search.
- Agent cards already use `AgentSelectionCard`, the same reusable card used by `AgentSwitchDialog`.
- `AgentSwitchDialog` already has search, tag filtering, card click selection, favorite handling, and compact card layout. It returns one selected agent immediately, so team membership needs a related but separate multi-select dialog.
- BaseLib exposes a shared `TreeView` component and the repo already uses it in workflows, processes, projects, and workbench support panels.

## Team Persistence Gap

- `SandboxWorkspaceCatalog` currently contains agents, providers, capabilities, and memory only.
- There is no `AgentTeam` model, team list, team editor API, or membership mutation API.
- Agent deletion prunes agent-scoped execution records, memory, chat, logs, metrics, approvals, artifacts, checkpoints, and receipts, but cannot yet prune team memberships.

## Process Launch And HR Matching

- Process launch planning already creates launch plans, roles, and candidates. Candidate facts are stored in `ProcessLaunchCandidate`, including `TechnicalAgentId` and `MetadataJson`.
- `ProcessesService.MatchLaunchPlanWithHrManagerAsync` exists and can recalculate role selections, but the UI does not expose an HR-match action or team selector.
- `ProcessWorkspaceRunsLaunchSection.razor` renders the role candidate matrix inline inside the launch plan detail surface.
- Candidate view models currently do not expose team fit or out-of-team markers. Metadata JSON can carry team-match flags without a process database migration.

## Shared Component Decisions

- Use BaseLib `TreeView` for the teams/agents navigation.
- Use BaseLib `Grid`, `Stack`, `Split`, `Cluster`, `SelectionListItem`, `StatusBadge`, and existing `AgentSelectionCard` for the UI.
- Add custom CSS only for panel-specific arrangement and stable sizing that shared component parameters do not cover.
