# Conversation Components Boundary

## Status

Accepted and implemented.

## Context

Agent and ordinary-chat surfaces reuse participant, transcript, composer, catalog, and floating-window presentation. That presentation must not make ordinary conversations depend on agent tools, skills, execution, approvals, voice, provider runtimes, or persistence.

`CanDoItAll.AppComponents` is source-neutral but owns a broader application facade and FileTools dependencies. Placing conversation presentation there would add unrelated coupling.

## Decision

`src/UI/CanDoItAll.Conversations.Components` owns the backend-neutral conversation presentation boundary:

- immutable presentation records;
- participant cards, lists, and pickers;
- conversation thread, transcript, message, and Markdown presentation;
- composer presentation;
- reusable conversation identity and runtime fields;
- catalog and floating-window presentation seams;
- active-chat lifecycle field presentation;
- isolated bUnit tests.

The project renders typed state and emits typed user intent. Product and Agent Framework owners adapt their domain/runtime state into these contracts. Opaque keys are never interpreted inside the presentation boundary.

## Dependency direction

Agent Framework components, Simple Chats components, and the shared conversation shell may depend on `CanDoItAll.Conversations.Components`. The neutral project may depend on Blazor and focused shared component libraries, but it must not reference Agent Framework, product modules, backend services, Web, EF Core, persistence, or provider SDKs.

## Rejected alternatives

Adding ordinary-chat branches to existing agent components preserves backend coupling and creates source-switch components. Moving all agent components into `AppComponents` mixes conversation presentation with approvals, execution, voice, tools, context, and runtime policy. Interfaces or partial classes around an agent chat panel change file shape without creating independent ownership or testability. Locating reusable UI in a product module reverses dependency direction.
