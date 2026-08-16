# ADR-001: Application-owned neutral Conversation Components boundary

## Status

Proposed; accepted only after CP1 dependency evidence.

## Context

Current reusable chat/list/settings markup is located in AgentFramework projects and exposes Agent domain/runtime types. Future Simple Chat UI must reuse presentation without acquiring agent tools, skills, execution, approvals, voice, or runtime services.

`CanDoItAll.AppComponents` is source-neutral but already owns a broad app facade and FileTools dependencies. Adding all conversation work there would increase unrelated coupling for MAF components.

## Decision

Prefer a focused Razor project:

`src/UI/CanDoItAll.Conversations.Components`

It owns:

- source-neutral presentation records;
- participant cards/lists/pickers;
- conversation thread presentation;
- transcript/message/markdown presentation;
- composer presentation;
- reusable conversation-definition identity/runtime fields;
- floating catalog/window presentation seams;
- generic active-chat lifecycle field presentation;
- isolated bUnit tests.

AgentFramework components and modules map existing Agent records into this boundary.

## Dependency direction

`Modules.AgentFramework`
→ `AgentFramework.Components`
→ `Conversations.Components`
→ `CanDoItAll.Components.BaseLib`

The neutral project does not point back to AgentFramework, Modules, Infrastructure, Web, or LlmChats.

## Rejected alternatives

### Put Simple Chat branches into existing Agent components

Rejected because it preserves the backend dependency and creates source-switch components.

### Move all Agent components into AppComponents

Rejected because approvals, execution, voice, tools, context, and runtime policy are not source-neutral.

### Add interfaces implemented by AgentChatPanel

Rejected because that is facade-only extraction and does not create independent ownership or testability.

### Use partial classes to split AgentChatPanel

Rejected because file separation does not reduce responsibility concentration.

### Put reusable UI in Modules.LlmChats

Rejected because it reverses dependency direction and makes Agent UI depend on the Simple Chat product module.

## Fallback

Use `src/UI/CanDoItAll.AppComponents/Components/Conversations` only when CP1 records concrete dependency or build evidence that the focused project is invalid. The fallback must still satisfy all forbidden dependency and testability rules.
