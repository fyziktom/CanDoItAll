# Page Inputs: Agents And Workflows

## PI-AGENTS Agents `/agents`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\CapabilityProofPanel.razor`

Current display:
- `PageScaffold` with summary tiles `Technical agents`, `Providers`, `Bound resources`, `Capabilities`, `Active runs`, `Failed runs`.
- Secondary tabs render providers, agents, chat, capabilities, governance, scenarios, and diagnostics.
- Header actions include `Feed defaults`, `Open CRM / HR agents`, `Open workflows`, and `Open processes`.

Current UX flows:
- User scans runtime health, feeds defaults, opens CRM/HR projection, switches to workflows/processes, manages provider profiles, technical agents, chat, capabilities, governance, scenario harness, and diagnostics.
- Agent catalog opens `AgentDetailsDialog`; chat opens switch/runtime/log dialogs.

Target proposal:
- Use `04-agent-workflow-tabs-dialogs-proposal.png` panels 1-5.
- Keep summary strip compact; show tab bodies as dense list/detail workspaces with action toolbar.

Function coverage confirmation:
- Covers all summary tiles, header actions, secondary tabs, provider/agent/chat/capability/governance/scenario/diagnostic workspaces.
- Improves professional scanability without hiding runtime status.

## PI-AGENT-DETAILS AgentDetailsDialog Tabs

Source reference:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor`

Current display:
- Dialog with tabs `Identity`, `Runtime`, `Project Structure Access`, `Workspace Tools`, `Secrets`, `Process Access`, `Skills and MCP`, and `Tags`.
- Edits agent identity/runtime/provider/owner/visibility/system prompt/temperature/tokens/tags and access settings.

Current UX flows:
- User opens agent record from catalog, edits identity/runtime/access/tooling/secrets/process/skills/tags, saves or cancels.

Target proposal:
- Use `04-agent-workflow-tabs-dialogs-proposal.png` panel 3.
- Dense inspector dialog with tab strip, left identity context, main tab form, and footer actions.

Function coverage confirmation:
- Covers every real AgentDetailsDialog tab.
- Uses a reusable dialog pattern suitable for other large dialogs.

## PI-AGENT-CHAT-RUNTIME Agent Chat And Runtime/Log Dialogs

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentChatPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ChatWorkspacePanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Components\ContextualAgentWorkspaceWindows.razor`

Current display:
- Chat workspace panel with conversation list, message transcript, agent switch, runtime details, execution log dialog, thread history dialog, and runtime details dialog.

Current UX flows:
- User chooses/switches agent, sends messages, opens runtime details/logs/history.

Target proposal:
- Use `04-agent-workflow-tabs-dialogs-proposal.png` panel 4.
- Keep conversation list and transcript visible; runtime/log dialogs become compact inspectors.

Function coverage confirmation:
- Covers chat, agent switch, runtime detail, execution log, and history flows.
- Keeps the flow video-friendly by avoiding large explanatory cards.

## PI-WORKFLOWS Workflows `/agents/workflows`

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`

Current display:
- `WorkflowsPage` has tabs `Dashboard`, `Workflows`, `Editor`, `Templates`, `History`, and `Analytics`.
- Summary tiles include `Definitions`, `Runs`, `Pending input`, `LLM components`, and `Default backend`.
- Actions include `Refresh`, `Create starter`, `Open agents`, `Run test`, `Cancel selected run`, `Respond`, paging, detail buttons, and canvas editor operations.

Current UX flows:
- User selects workflow definition, creates starter, edits workflow canvas, runs test, responds to waiting input, cancels run, reviews history and analytics, opens run/event details.

Target proposal:
- Use `04-agent-workflow-tabs-dialogs-proposal.png` panels 6-8.
- Add workflow TreeView grouped by definition/version/status/components/recent runs; each tab body gets a dense layout rather than full-width cards.

Function coverage confirmation:
- Covers all tabs, summary tiles, create/run/respond/cancel/detail flows, and workflow canvas editor.
- Adds the required tree grouping for workflow definitions.

## PI-WORKFLOW-DIALOGS Preview Inputs, Workflow Run, Event Detail, Canvas Editor Dialogs

Source references:
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\WorkflowCanvasEditor.razor`

Current display:
- `Preview inputs` dialog collects runtime input for a selected workflow.
- `Workflow run` dialog shows run id, state, backend, timestamps, summary, events, result payload, and diagnostic preformatted details.
- Event detail dialog shows event kind/time/run and payload details.
- Workflow canvas editor opens component creation/edit dialogs.

Current UX flows:
- User opens input dialog to run preview, opens run details from history, opens event detail from timeline, edits workflow component data.

Target proposal:
- Use `04-agent-workflow-tabs-dialogs-proposal.png` panel 8.
- Inspector-style side-by-side dialogs with compact facts, timeline, payload preview, and footer actions.

Function coverage confirmation:
- Covers all workflow dialogs and runtime details.
- Improves readability of dense payload/event information.
