# UI composition contract

## Page hierarchy

AgentFramework page:

- PageScaffold and PageHeader
- SecondaryTabs
  - Overview
  - Agents
  - Simple Chats
    - Conversations
    - Definitions
  - Providers
  - remaining existing tabs

The Simple Chats component supplies only its workspace body and inner tabs.

Definition setup is a Wide dense-chrome Dialog with internal ModalCompact Tabs:

- Identity: name, summary, system prompt, tags, avatar preview/actions;
- Runtime: Chat provider/model, temperature, thinking effort, timeout, model parameters;
- Output and revision: response format/schema, status/revision context, lifecycle transitions, revision reason.

The validation summary stays above the tab panels and the stable Cancel/Save footer stays outside the dialog-body scroll owner.

Agent and Simple Chat identity editors compose one shared AgentFramework avatar selector. The selector owns current preview, bundled options, default reset, validated browser upload, and explicit AI-generation UI state. A typed host gateway supplies provider/model availability and generation; the selector never persists either aggregate.

## Usage hierarchy

Overview usage section:

- scope selector: Both, Agents, Simple Chats;
- scoped usage/token/cost/unpriced metrics;
- scoped provider bar and distribution charts;
- scoped model data;
- source-appropriate consumer panels;
- provider/model/consumer detail actions that receive the same selection.

Catalog metrics remain outside the scope selection.

## Route state

- tab=simple-chats selects the feature.
- simpleChatView=conversations|definitions selects inner mode.
- definitionId and conversationId use existing strong-ID parsing.
- usageScope=both|agents|simple-chats selects dashboard usage.
- invalid or unauthorized IDs fail predictably and never expose another database profile.

SB01 may adjust exact query names only if it records the collision inventory and updates every requirement/test before implementation.

## Desktop proof

At 1600x1000 capture:

- Agent overview, Both selected;
- Agent-only and Simple-Chat-only scoped charts;
- provider/model/consumer detail dialog;
- Simple Chats Conversations and Definitions modes;
- each definition-editor settings tab;
- Agent and Simple Chat shared avatar selector open, including deterministic AI success and unavailable/error state;
- main Simple Chat completed/streaming/cancelled state;
- floating Agent and Simple Chat open;
- hide/reopen/reload state;
- /chats redirect result.

Each proof records URL, viewport, selected state, console/page errors, and screenshot path.
