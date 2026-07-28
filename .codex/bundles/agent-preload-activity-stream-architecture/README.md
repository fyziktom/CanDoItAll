# Agent Preload and Activity Stream Architecture

This bundle is a coordination and execution package for `agent-preload-activity-stream-architecture`.

## Profile

- `initiative`

## Mission

- Make agent chat visibly responsive before an execution run exists, reuse current module state through immutable revisioned snapshots, and reduce verified backend startup latency without pooling mutable runtime resources or creating a second source of truth. Establish a typed in-process activity stream that is safe for module consumers now and for an authorization-aware SSE projection later.

## Outcome Contract

- Requested outcome: floating agents and process-manager agents expose accurate preparation activity immediately, start from already-loaded project/process state, and reach model/tool execution faster.
- Hard constraints: backend first; UI begins only after measured backend improvement; strongly typed contracts; no string-key cache; no silent fallback; immutable snapshots with explicit revision/freshness; no parallel use of a shared `DbContext`; no live MAF/provider/MCP instance pooling unless lifecycle proof later justifies it; SSE is projection-ready but out of scope.
- Evidence required before closure: architecture snapshots and gate review, failing-first and passing tests, producer/consumer/lifecycle proof, baseline-versus-after timing artifacts, concurrency and stale-revision adversarial tests, large-screen Playwright screenshots for both chat surfaces, full build/test proof, one real `gpt-5.4-mini` agent validation, SharedInfo documentation/skill updates, and a healthy restarted host on port 5032.
- Known blockers or explicit scope exceptions: external SSE endpoints, MQTT/OPC UA projections, distributed event transport, generic application caching, and mobile UI work are not included.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries when architecture decisions are material
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts when repeated handoff needs them
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-current-state-baseline-and-architecture-contracts`
2. `subbundles/02-02-typed-agent-activity-stream-foundation`
3. `subbundles/03-03-revisioned-runtime-preparation-snapshots`
4. `subbundles/04-04-module-runtime-context-snapshot-adapters`
5. `subbundles/05-05-backend-performance-and-concurrency-gates`
6. `subbundles/06-06-blazor-agent-activity-feedback`
7. `subbundles/07-07-documentation-api-skills-and-runtime-closure`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## UI Target Policy

- CanDoItAll applications target large-screen desktop use; do not add small/medium/mobile tuning unless explicitly requested.
- Reusable basic `CanDoItAll.Components.BaseLib` components remain responsible for small, medium, and large viewport behavior.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Complete — SB01 through SB07`
- Subbundle gate review: `A1-A7 GO; three inherited A5 P2 follow-ups remain explicit`
- Final closure gate: `A7 GO with follow-up`
- Browser validation analytics: `Pass — seven reviewed 1920x1080 states, zero console errors/warnings`
