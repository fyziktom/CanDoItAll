# Original request

Received 2026-08-17.

> I need you to improve our implementation of llmchats due to architecture consolidation with agent modules.
> the chats are kind of simplified way how to chat with llm and it has logical connection to agent module. thats where user setup some provider and then define chat or agent.
> also it matters due to watching costs. it should be together with agent costs. we already have good dashboard in agent module. It should be switchable to show just agents, just chats or both.
>
> on the other hand I checked the code and there are lots of classes so just adding them into agent module would not be right. we should isolate some parts that can be as libraries to create basic chats abstractions, helpers, components etc. and then those will be added into agent module. The simple chats should be another tab next to Agents tab in agents page. The CanDoItAll.Modules.LlmChats is kind of light. most responsibility takes CanDoItAll.Modules.LlmChats.Persistance. So it is already little separated, just naming is not good because it brings all projects around LlmChats in solution under Modules namespace. They should be part of MAF namespace because they are related to providers.
>
> It will be larger work. I need you to prepare detailed bundle for it. do not start implementation, just prepare new bundle for it.

Canonical interpretation and binding assumptions are in inputs/01-user-request.md.

