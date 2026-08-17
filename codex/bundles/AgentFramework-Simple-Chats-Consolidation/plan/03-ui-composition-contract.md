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
- definition editor;
- main Simple Chat completed/streaming/cancelled state;
- floating Agent and Simple Chat open;
- hide/reopen/reload state;
- /chats redirect result.

Each proof records URL, viewport, selected state, console/page errors, and screenshot path.

