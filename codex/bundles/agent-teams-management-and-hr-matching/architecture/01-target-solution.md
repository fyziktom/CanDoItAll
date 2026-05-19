# Target Solution

## Domain Model

- Add `AgentTeamDefinition` to AgentFramework models with `Id`, `Name`, `Description`, `AgentIds`, `CreatedAtUtc`, and `UpdatedAtUtc`.
- Add `AgentTeams` as an init property on `SandboxWorkspaceCatalog` and `SandboxWorkspaceDocument` so older JSON can deserialize safely.
- Normalize teams by trimming text, dropping empty/unknown agent ids, de-duplicating memberships, and sorting by team name.
- Prune deleted agent ids from team memberships during `DeleteAgentAsync`.

## Service Contract

- Extend `IAgentFrameworkWorkspaceService` with list/save/delete team operations and membership update operation.
- Implement those operations inside `AgentFrameworkWorkspaceCatalogService` using the same `UpdateCatalogAsync` pattern as agents and capabilities.
- Expose API endpoints under `/api/agents/teams` for callers that use HTTP, while Blazor components can use the injected service directly.

## Agents Tab UX

- Enhance `AgentCatalogPanel` with a two-region layout: a left team tree and right card grid.
- Use BaseLib `TreeView` for all agents, teams, and child agent nodes.
- Add create/edit/delete/manage actions for teams in the Agents tab.
- Add `AgentTeamMembershipDialog` that uses `AgentSelectionCard` in multi-select mode, with search and selected count, then returns selected agent ids on confirm.
- Keep the existing card grid and agent details dialog behavior intact.

## Process HR Matching UX And Semantics

- Add an HR matching dialog/control in launch planning that lists agent teams and runs `MatchLaunchPlanWithHrManagerAsync` with an optional team id.
- Load teams through AgentFramework workspace service in the process workspace.
- Extend the matching request object/service signature with `AgentTeamId`.
- During matching, if a team is selected, resolve the team's technical agent ids and mark candidates as in-team or outside selected team based on `ProcessLaunchCandidate.TechnicalAgentId`.
- Prefer in-team AI candidates with a scoring boost, but allow out-of-team candidates to remain selectable/recommended when they are the best available fit for a required role.
- Store team fit markers in `ProcessLaunchCandidate.MetadataJson` and expose them through `ProcessLaunchCandidateViewModel`.

## Boundaries

- Do not model teams as CRM-HR `Party` or `ProjectPartyAssignment` records in this bundle.
- Do not change provider/profile/secret ownership.
- Do not rewrite launch planning or process execution; add team-aware matching through the existing matching path.
