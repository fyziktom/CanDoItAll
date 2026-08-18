# Non-goals

- Do not add Agent tools, skills, approvals, Memory, voice, attachments, workflows, Project Structure context, or arbitrary context attachment to Simple Chats.
- Do not redesign Simple Chats as Agents or persist fake Agent identifiers.
- Do not create a second provider configuration/catalog for Simple Chats.
- Do not build a new transactional central usage ledger, event bus, or outbox in this initiative.
- Do not dual-write Simple Chat usage into the Agent file store.
- Do not merge Agent and Simple Chat usage only in Razor/UI code.
- Do not rename LlmChats_* database tables, historical migrations, API routes, API scopes, or transfer module identity.
- Do not rewrite existing generic AgentFramework.Llm.* or Conversations.* libraries unless an owned requirement needs a narrow additive change.
- Do not turn the known Infrastructure dependency in AgentFramework.Llm.Conversations into a side refactor.
- Do not add mobile or tablet layout work; product UI proof targets desktop 1600x1000.
- Do not add a permanent compatibility assembly for CanDoItAll.Modules.LlmChats namespaces.
- Do not run the full unfiltered Playwright suite; use the named consolidation selectors and Playwright MCP scenarios.
- Do not implement any of the above during bundle preparation.
