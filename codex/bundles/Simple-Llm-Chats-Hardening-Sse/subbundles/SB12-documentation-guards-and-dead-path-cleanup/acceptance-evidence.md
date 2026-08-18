# Acceptance evidence — SB12

For each criterion, provide behavioral/source evidence rather than only a test count.

- [x] No production path uses the independent-context UoW or synchronous request-owned provider execution.
- [x] No Razor, floating-chat, shared-component, Project Structure context, or UI integration was added.
- [x] Executable guards enforce dependency direction and prevent agent/tool/skill/MCP leakage.
- [x] Authoritative docs accurately describe asynchronous operation and SSE contracts.
- [x] Future UI, context, and enterprise deployment bundles have explicit ownership handoffs.
- [x] All proof and closure records reference the actual implementation head.

## Required semantic proof

- Intended case: durable admission returns `202`; the hosted dispatcher executes provider work; Web
  replays committed event sequences through the shared SSE writer; deferred UI/context/deployment work
  remains outside the product.
- Negative/race/crash/failure case: the guard rejects independent transcript contexts, request-owned
  engine execution, endpoint-local SSE, UI/Razor changes, forbidden agent/tool/skill/MCP dependencies,
  and dormant deployment fields.
- Why the old implementation would fail this proof: reviewed feature commit
  `16b6aa4b60dc88a6134dd6c9c9e634c064ac5847` directly awaited
  `conversationEngine.SendAsync` and created independent contexts inside `EfLlmConversationStore`.
- Exact source owner: LLM Chats product/application contracts; Persistence EF/provider adapters;
  Composition hosted dispatch; Web HTTP/SSE transport; repository documentation and bundle guards.
- Exact command(s): documentation, architecture, SSE, bundle, traceability, test-policy, and diff
  validators recorded under `proof/SB12/transcripts`; no test or build command was required.
- Actual result: 181 maintained Markdown files pass; every source/bundle guard passes; no production C#
  or UI file changed; current-source shallow-path searches return no match.
- Evidence artifact: `proof/SB12/manifest.md`, semantic invariants, and transcripts.
- Commit SHA: `58265975e868731e25e39d4bf9109f6010d68127`.
