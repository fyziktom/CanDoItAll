# 02-agents-page-tree-and-team-management-ui

## Status

- `Completed`

## Objective

- Add Agents tab team management, team/agent tree navigation, team filtering, and a multi-select membership modal based on agent cards.

## Covered Inputs

- `N003`: Team creation must be in the Agents module.
- `N004`: Tree view on Agents tab shows teams and agents under teams.
- `N005`: Clicking a team filters agents.
- `N006`: Add agents to team via modal with multi-selection by clicking agent cards.
- `N007`: Modal should follow the switch-agent card pattern.
- `N008`: Agents can be in multiple teams.

## Prerequisites

- Subbundle 01 closure gate has passed.
- BaseLib `TreeView`, `Grid`, `Stack`, and `AgentSelectionCard` usage has been inspected.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor.css
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSwitchDialog.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\AgentSelectionCard.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs

## Deliverables

- Team tree in `AgentCatalogPanel` using BaseLib `TreeView`.
- Team create/edit/delete actions in the Agents tab.
- Team node selection that filters the agent card grid.
- Multi-select team membership dialog using `AgentSelectionCard`.
- Component tests for filtering and membership updates.

## Dependency Impact

- Browser proof and raw note closure depend on this UI matching the architect's requested management flow.
- Process matching can be implemented without this UI, but user confidence depends on seeing and managing teams in the Agents tab.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Load agents and teams together in `AgentCatalogPanel`.
2. Build `TreeViewNode` data for all agents and teams.
3. Add selected tree node state and use it to filter visible agent cards.
4. Add team create/edit/delete controls.
5. Add `AgentTeamMembershipDialog` with card multi-select and confirm/cancel.
6. Add component tests and browser proof.

## Scope Exceptions

- Do not redesign the full Agents page header or non-agents tabs.

## Do Not Do

- Do not replace the existing agent details dialog.
- Do not hand-roll tree behavior instead of BaseLib `TreeView`.
- Do not make membership single-select.

## Acceptance Checklist

- All agents view shows the full catalog.
- Team node click filters the visible card grid.
- Agent child click selects or opens the matching agent without losing team filter.
- Membership modal can select more than one agent before confirming.
- Same agent can appear under multiple teams.
- Empty team state is readable.

## Proof Required

- Component test for tree filtering and membership modal.
- Browser proof at `/agents?tab=agents` in large viewport.
- Screenshot of membership modal open state with selected cards.
- Narrow viewport follow-up if tree/card layout changes responsively.

## Browser Validation Logging

- Route: `/agents?tab=agents`.
- Viewports: large desktop first, then a narrower pass when layout is affected.
- Actions: load page, create/select team, open membership modal, select multiple agent cards, confirm, select team node, inspect filtered grid.
- Screenshots: `codex/bundles/agent-teams-management-and-hr-matching/evidence/agents-team-tree-desktop.png` and `codex/bundles/agent-teams-management-and-hr-matching/evidence/agents-team-membership-modal.png`.
- Review questions: tree labels readable, modal not clipped, selected cards obvious, buttons fit, no overlapping text.

## Progression Gate

- Passed. Component test proves tree filtering and membership updates; browser proof captured the team tree, team details modal, and membership modal open state.

## Closure Evidence

- Passed: `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~AiAgentsPageTests.Agent_catalog_team_tree_filters_agents_and_member_modal_updates_membership"`
- Browser evidence:
  - `codex/bundles/agent-teams-management-and-hr-matching/evidence/agents-team-tree-desktop.png`
  - `codex/bundles/agent-teams-management-and-hr-matching/evidence/agents-team-details-modal.png`
  - `codex/bundles/agent-teams-management-and-hr-matching/evidence/agents-team-membership-modal.png`

## Suggested Agent Prompt

```text
Implement only the Agents tab team tree and management UI. Reuse BaseLib TreeView and AgentSelectionCard, preserve existing card grid and details dialog behavior, add focused tests, capture browser proof, update the execution report, and stop if the modal or tree cannot be proven visually.
```
