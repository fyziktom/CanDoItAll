# CP1 — Neutral boundary review

## Ownership

- [x] focused neutral project created or approved fallback documented
- [x] responsibility is cohesive and not a dumping ground
- [x] no Agent/LlmChats/backend/persistence types
- [x] no service location
- [x] no boolean god component
- [x] contracts use opaque keys and source-neutral presentation records
- [x] isolated tests run without full Agent runtime

## Dependencies

- [x] before/after CodeAnalytics graph recorded
- [x] no cycle
- [x] only planned project references
- [x] neutral project points only to approved source-neutral UI dependencies
- [x] source guard passes

## Compatibility

- [x] production consumers not migrated prematurely
- [x] current Agent public entry points remain
- [x] no visible behavior added

## Decision

- [x] pass to SB03
- [ ] reopen SB02
- [ ] use documented fallback
- [ ] repair architecture

Rationale: `CanDoItAll.Conversations.Components` has no project references and only Blazor/BaseLib packages. It owns validated opaque keys, immutable badge/meta contracts, and directly tested BaseLib badge composition; it is not a type bucket. AgentFramework.Components points inward to the neutral project, production consumers remain untouched, direct tests discover/pass 7/7, and the after snapshot has no project cycle. The impact analyzer's broad promotion is preserved for SB09 because the bundle explicitly forbids broad execution in SB02.
