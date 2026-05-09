# Current State

- The modal is fullscreen and already opens from the project-structure Start flow.
- The workspace currently renders all role cards in one summary grid; selected role state only changes highlighting and the bottom selected-agent panel.
- Manual assignment already reuses `AgentSwitchDialog`, which includes search, tag filtering, and favorites.
- Launch candidates currently carry ids, labels, score, recommendation/availability summaries, and technical agent id, but not provider/model/tool/skill metadata.
- Agent catalog records expose provider profile id, model, tags, summary, role title, avatar url, and assigned capabilities. Capabilities include `Skill`, `McpServer`, `Tool`, and related kinds.
- The BaseLib `TooltipTarget` and `TooltipService` are available for compact badge tooltips.

## Implementation Implications

- The UI can add `All` by treating `selectedRoleId == null` as summary mode.
- Role-specific ranking can be implemented entirely client-side from the launch-plan candidate list.
- Metadata badges need parent-side enrichment from `IAgentFrameworkWorkspaceService.ListAgentsAsync`, `ListProvidersAsync`, and existing candidate technical agent ids.
