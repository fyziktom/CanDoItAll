# C# Testability Plan

- Presentation records: construct independently and prove defensive copies, key validation, and action dispatch.
- Transcript reducer: pure unit tests for duplicate cursors, deltas, terminal states, gap reset, cancellation, and recovery-required.
- Markdown: component/unit tests with hostile schemes and raw HTML sentinels.
- UI gateways: fake application services/session factory; no Web host required for component behavior.
- ActiveOperationId: real PostgreSQL/application/API integration tests because profile fencing and durable admission matter.
- Definition editor: component tests plus real application conflict/capability integration.
- Main page: bUnit for composition; targeted Playwright for real circuit, scroll, dialogs, and streaming.
- Floating contributors: instantiate each contributor independently; neutral shell tests use fake contributors.
- Agent parity: existing Agent tests remain consumers; targeted browser checks verify settings, contextual chat, history, close, and affinity.
- Final broad gate: exactly once after all targeted proof passes.
