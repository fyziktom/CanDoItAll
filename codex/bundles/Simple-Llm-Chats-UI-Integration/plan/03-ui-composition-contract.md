# UI Composition Contract

## Main Page

- Route: `/chats` after SB10 activation.
- Primary task: conduct a Simple Chat conversation.
- Supporting task: manage reusable definitions in a separate top-level tab, not stacked beneath the transcript.
- Conversations tab: bounded left thread rail plus one dominant workspace panel; start-chat uses a dialog/picker.
- Definitions tab: list/catalog plus wide editor dialog; no permanently open editor beneath the list.
- Stats: compact badges only; no dashboard tile wall.
- Textareas: system prompt and JSON schema receive intentional wide/tall editor space.
- Scroll ownership: page/root owns no competing scroll; rail and conversation flow own their bounded internal scrolling.

## Floating Catalog

- One catalog window with lifecycle tabs `Available` and `Active`.
- Kind filter is separate: `All / Agents / Chats`.
- Focused windows retain source-owned content and actions.
- Agent-only context badges are not shown for Simple Chats.

## First Viewport Target

At 1600x1000, the first viewport must show:

- route/page identity;
- thread list/search and new-conversation action;
- selected chat identity and visible recent transcript;
- composer and send/cancel state;
- no lateral page overflow;
- no hidden primary action behind independent page scrolling.
