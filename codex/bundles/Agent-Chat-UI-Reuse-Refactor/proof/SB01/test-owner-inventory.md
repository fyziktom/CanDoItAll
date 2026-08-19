# SB01 owner-test inventory

- `AgentCatalogPanelTests.cs`: card interaction separation, stable status/type/provider presentation, catalog launcher delegation, toolbar semantics, exact selection, delete behavior.
- `AgentCompactListTests.cs`: dense rows, selection state, icon actions, typed callbacks.
- `AgentChatPanelResponsivenessTests.cs`: persisted run selection, cross-thread and ABA guards, non-blocking sends/approvals, post-run refresh, secret-masked logging, context capture, reopened run refresh.
- `AgentPanelSelectionFailClosedTests.cs`: missing-agent fail-closed behavior and exact managed-agent identities.
- `ChatWorkspacePanelTests.cs`: execution stream/history dialogs, editable thread title, audio controls, image upload visibility, composer callback.
- `AgentChatModalTests.cs`: participant picker filtering/favorites, runtime details, thread history, hidden automatic context.
- `FloatingAgentChatHostLifecycleTests.cs`: pending initialization remains non-blocking and is cancelled on disposal.
- Contextual and Processes ownership is additionally covered by their component suites and by direct `ChatWorkspacePanel` usage.

Focused environment proof:

- Expected discovery: 1.
- Actual discovery: 1.
- Test: `CanDoItAll.Tests.Components.AgentFramework.ChatWorkspacePanelTests.Running_execution_log_renders_compact_chat_stream_and_opens_details_dialog`.
- Result: passed, 1/1, 347 ms.

No unfiltered Components, Stable, Playwright, or full-solution test suite was run for SB01.

