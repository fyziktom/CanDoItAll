# Normalized Requirements

| Id | Requirement | Source notes | Acceptance signal |
| --- | --- | --- | --- |
| R001 | Agents tab must replace the inline list/detail editor with a card-led Agents surface. | N001, N002 | `/agents?tab=agents` renders agent cards. |
| R002 | The switch-agent modal and Agents tab must use the same reusable agent-card component. | N002 | `AgentSwitchDialog` and `AgentCatalogPanel` render `AgentSelectionCard`. |
| R003 | Double-clicking an agent card on the Agents tab must open a DialogService modal. | N003 | Component/browser proof can trigger `ondblclick` and observe the dialog. |
| R004 | The dialog must preserve the existing technical editor fields and save/delete behavior. | N004 | Existing identity/runtime/access/tags fields remain editable and SaveAgentAsync/DeleteAgentAsync paths still work. |
| R005 | The dialog editor must be split into tabs, with Identity, Runtime, Project Structure Access, Workspace Tools, Process Access, Tags, and Skills/MCP tabs. | N005, N006 | Modal uses BaseLib `Tabs` and each editor section is separately reachable. |
| R006 | The Skills/MCP tab must show connected capabilities and available capabilities with assign/remove actions. | N006 | Attached skills/MCP servers are labeled; available catalog items can be assigned from the dialog. |
| R007 | Summary and Instructions must use the full available modal width and larger default heights. | N007, N008 | Identity tab text areas are full-width and visibly taller than compact defaults. |
| R008 | Existing cross-module deep links to `/agents?tab=agents&agentId=...` must remain useful. | N003, N004 | Route-driven selected agent opens or exposes the dialog editing context. |
