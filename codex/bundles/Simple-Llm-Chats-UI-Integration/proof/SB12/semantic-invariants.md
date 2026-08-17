# SB12 Semantic Invariants

## Composition

- Any host that registers either conversation feature module receives the neutral launcher and coordinator registrations required by that module's contributor/facade.
- The neutral shell merges descriptors and delegates typed actions; it never calls Agent, LlmChats, provider, or persistence backends directly.
- Web remains the application composition root and renders one unified shell host.

## Main And Floating Simple Chats

- Main and floating surfaces use the same durable conversation/application operations and canonical transcript projection.
- Starting, reopening, hiding, archiving, and cancelling are distinct typed actions. Hiding never implies cancellation.
- Simple Chats receive no Agent affinity or ambient Workbench context.
- A completed operation settles ready for another message and cannot duplicate the dispatched turn during follow/reopen.

## Floating Agents

- Agent context access and affinity remain fail-closed and owned by the Agent coordinator.
- On an allowed Agents surface, follow-current uses the published position; Detach remains explicit.
- Keep active hides the window without stopping its active handle or losing transcript state. Stop remains a separate destructive choice.

## Safety And Evidence

- Streaming chunk persistence and audited evidence do not share one EF `DbContext` concurrently.
- Errors shown to users are sanitized, sensitive values are not logged, and database-profile fences remain authoritative.
- Automated browser proof supplements component/application tests; non-green broad-gate results remain visible and cannot be rewritten as a pass.
