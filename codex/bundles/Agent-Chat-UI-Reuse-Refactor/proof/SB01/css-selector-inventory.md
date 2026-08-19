# SB01 CSS, DOM, and interaction inventory

Stable selectors that must remain behaviorally equivalent through migration:

- Shells: `agents-chat-panel`, `chat-workspace-panel`, `floating-agent-chat-host`, `floating-agent-chat-settings`.
- Participant selection: card/list shell, select, favorite, new-chat, and history IDs supplied through the existing typed `TestId` parameters; `agent-status-ribbon`; `agent-private-provider-badge`.
- Thread rail: `agent-thread-search`, `agent-thread-card`, `agent-thread-selected-agent`, `agent-switch-button`, `agents-chat-focused-new-thread`, `agents-chat-open-runtime-details`.
- Workspace: `agent-chat-run-state`, `chat-message-hidden-context`, `chat-execution-stream`, `chat-execution-summary`, `chat-execution-entry`, `chat-execution-history`.
- Composer: `chat-prompt-input`, `chat-send-button`, attachment/image/voice buttons and input, approval buttons.
- Floating lifecycle: `floating-agent-chat-search`, affinity status/toggle, pending context, close-dialog cancel/keep/stop actions, settings retention/maximum/save inputs.
- Definition editor: `agents-details-tabs` and the existing `agents-catalog-*` / `agents-details-*` identity, runtime, approval, capability, memory, voice, and workspace selectors.

Scroll and layering ownership at baseline:

- Catalog card grid: `max-height` plus `overflow-y:auto`; the desktop team rail is sticky and independently scrollable.
- Focused chat: the page shell owns a clamped desktop height; the thread rail owns vertical scrolling; `ChatWorkspacePanel` owns the conversation-flow scroll while the composer remains visible.
- Floating host: fixed overlay, `z-index:1700`; child workspaces use `min-height:0` and explicit internal scrolling.
- Settings dialog: dialog body owns scrolling and keeps actions visible.
- Selection card actions are outside the selection button; migration must not nest interactive controls.

Existing CSS, including the narrow desktop `Switch Agent` label wrap, is baseline behavior and is not opportunistically redesigned by this bundle.

