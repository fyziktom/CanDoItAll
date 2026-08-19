# Risk register

| Risk | Likelihood | Impact | Mitigation and gate |
|---|---|---|---|
| EF type/namespace moves generate destructive table renames | Medium | Critical | Preserve all LlmChats_* mappings and historical migrations; assert no rename/drop SQL and no unexpected pending model changes at SB05. |
| Runtime/persistence split weakens profile fencing or lease semantics | Medium | Critical | Characterize fresh scopes, DB generation, leases, heartbeat, commit fence, and concurrency first; prove through ports and PostgreSQL integration. |
| Usage gets counted from invocation, transcript, and terminal operation | High | Critical | Make OperationId + Ordinal the sole chat attempt source and add duplicate/retry negative tests. |
| Legacy zero-token rows are misreported as free | High | High | Introduce independent usage/pricing status; deterministic legacy policy and visible unpriced totals. |
| Current pricing is applied to historical calls | Medium | Critical | Persist immutable cost/hash/version for new rows; forbid query-time historical repricing. |
| Agent file and chat EF sources disagree or fail independently | Medium | High | Neutral source-result contract includes freshness/completeness/error state; compose without cross-store transaction and show partial failure explicitly. |
| Project extraction introduces a dependency cycle | Medium | Critical | Contract-first moves, before/after graphs, CodeAnalytics at CP0/CP1/CP2/CP4, no service location/reflection. |
| Old namespaces remain permanent facades | Medium | High | Caller inventory, no-new-caller guard, deletion in SB10; compatibility route is not a compatibility assembly. |
| Razor relocation breaks CSS isolation/imports/assembly discovery | Medium | High | Component tests and render proof before route activation; register assemblies exactly once. |
| /chats redirect loses inner state or loops | Medium | Medium | Typed query map, invalid-value tests, navigation history/back/forward Playwright proof. |
| Dashboard scope changes catalog totals or labels chats as agents | Medium | High | Separate catalog/runtime snapshot from neutral usage snapshot and use typed consumer rows. |
| Agent floating behavior regresses during shell registration cleanup | Medium | Critical | Existing component tests plus named main/floating Agent Playwright MCP parity before closure. |
| Large classes are only moved, not separated | High | Medium | Direct owner proof, no-new-partial check, old-owner shrink, targeted collaborator extraction for controller/engine/usage service. |
| Sensitive prompts/content appear in analytics/log proof | Low | Critical | Log/proof redaction requirement and source assertions; only IDs/status/counts in usage diagnostics. |
| Broad test run is consumed before candidate freezes | Medium | High | Stable is forbidden before SB11 and authorized exactly once against a recorded SHA. |

