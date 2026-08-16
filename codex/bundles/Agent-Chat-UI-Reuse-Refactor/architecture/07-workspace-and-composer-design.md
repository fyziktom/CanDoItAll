# Workspace, transcript, and composer design

## Neutral components

Suggested focused owners:

- `ConversationWorkspacePanel`
- `ConversationHeader`
- `ConversationTranscript`
- `ConversationMessageBubble`
- `ConversationComposer`
- `ConversationPromptTextArea`
- `ConversationMarkdownRenderer`

The legacy `ChatWorkspacePanel` remains the Agent-facing facade and composes these owners.

## Agent-owned slots

- execution activity and log;
- pending tool approvals and decisions;
- auto-approval controls;
- cancel/stop run;
- voice controls and activity;
- staged attachments and upload;
- prompt gallery;
- runtime detail dialog;
- agent permissions and status;
- context/affinity controls.

## Hidden context

Current UserRequestMarker parsing remains outside the neutral project. The adapter emits:

- visible user text;
- optional explicit context summary/detail;
- copyable content according to current behavior.

The neutral transcript never scans for Agent marker formats.

## Markdown

The neutral renderer may own Markdig with HTML disabled. Existing `ChatMarkdownRenderer` may delegate to it for compatibility.

## Re-render and future streaming seam

The transcript must correctly re-render when the supplied message content changes and must not cache message HTML by object reference in a way that blocks later incremental updates.

Do not add:

- SSE connection code;
- operation polling;
- stream cursors;
- Simple Chat event models;
- transient DB semantics.

## Behavior invariants

- send button state and keyboard behavior;
- draft preservation;
- focus and scroll;
- safe markdown;
- message role labels;
- timestamps and token estimates;
- copy behavior;
- errors and status;
- attachment/voice/prompt actions;
- approvals and execution controls;
- runtime detail dialog;
- cancellation semantics.
