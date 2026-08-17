# UI composition

## Canonical page

/agents owns the PageScaffold and PageHeader.

Top-level SecondaryTabs order:

1. Overview
2. Agents
3. Simple Chats
4. Providers
5. Voice
6. Floating chat
7. Chat
8. Capabilities
9. Governance
10. Diagnostics

The exact existing labels after Providers remain unchanged unless component evidence requires a narrow accessibility correction.

## Simple Chats tab

- Render a reusable workspace body, not LlmChatsPage.
- Preserve inner Conversations and Definitions Tabs.
- Keep provider selection sourced from Chat-purpose profiles configured in Providers.
- Preserve definition catalog/editor, conversation selection/archive/rename, streaming/cancel/reconnect/recovery, and floating contributor behavior.
- Use typed query state for inner tab and recognized IDs.

## Simple Chat definition settings dialog

- Use a Wide dense-chrome Dialog with BaseLib Tabs in ModalCompact mode and one dialog-body scroll owner.
- Internal tab order is Identity, Runtime, Output and revision.
- Identity contains name, summary, system prompt, tags, avatar preview, and the shared Choose avatar/default actions.
- Runtime contains Chat-purpose provider/model selection, temperature, thinking effort, timeout, and model-parameter JSON.
- Output and revision contains response format/schema, current status/revision context, revision reason, and lifecycle transitions.
- Validation is summarized above the tab panels and field-specific state remains adjacent to its owning control; saving with a hidden-tab error activates/focuses the owning tab when practical.
- Header/status and the stable Cancel/Save footer remain usable while the dialog body scrolls.

## Shared avatar selector

- Both Agent and Simple Chat editors use the one selector from CanDoItAll.AgentFramework.Components.
- It provides current preview, bundled choices, default reset, PNG/JPEG/WebP/GIF upload capped by AgentAvatarImagePolicy, and AI generation through a typed host gateway.
- AI availability identifies the configured image provider/model without exposing secrets. Loading, unavailable, validation, and generation error states are explicit.
- Closing the selector stages the value in the editor only; Cancel discards it and Save persists it through the existing Agent or Simple Chat mutation.

## /chats compatibility

- Redirect to /agents?tab=simple-chats.
- Map only recognized inner tab/definition/conversation query keys.
- Drop unknown keys predictably.
- No PageScaffold, workspace rendering, shell navigation contribution, or second registration.
- Prove no redirect loop and correct browser history/back/forward.

## Usage scope

- Visible near the usage metrics/charts in the first desktop viewport.
- Options: Both (default), Agents, Simple Chats.
- One typed selection instance drives overview usage data and every provider/model/consumer dialog.
- Scope changes cancel or supersede stale loads.
- URL behavior is explicit: usageScope=both|agents|simple-chats; invalid values normalize to Both with no exception/loop.

## Components

- SecondaryTabs for page peer modes.
- Existing Tabs for Simple Chat inner modes.
- Existing BaseLib selection control selected through Components MCP.
- CdaChart for provider/model charts.
- Existing compact metrics, StatusBadge, DataGrid, Dialog, Empty/Loading/Error components.
- No raw replacement control composed from unwrapped div/button elements.

## Layout/accessibility

- Product viewport: 1600x1000.
- One page/workspace scroll owner.
- Dialog body scroll remains inside the dialog.
- Floating windows stay above page content and below modal overlays.
- Selected tab/scope, labels, keyboard operation, focus restoration, loading, empty, unknown, unpriced, partial-source, forbidden, and error states are testable.
- Capture normal page, scope selector open/changed, each Simple Chat settings tab, shared avatar selector in Agent and Simple Chat contexts, AI unavailable/success state, Simple Chat editor/dialog, Agent floating chat, Simple Chat floating chat, and usage detail dialog screenshots.
