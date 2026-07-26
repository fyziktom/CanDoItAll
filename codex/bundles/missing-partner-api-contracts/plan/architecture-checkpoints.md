# Architecture Checkpoints

## Preparation Gate

- Snapshot `snap-20260725222007-d4d57050` is healthy enough for scoped evidence.
- Exact production files are named.
- No new partial-class boundary is planned.

## After SB01-SB03

- Focused agent services are directly testable.
- `AgentsApi.cs` does not own archive/schema/idempotency policy.
- Project dependency graph has no new cycle.
- Existing broad workspace service is a thin facade for new responsibilities.

## After SB04-SB05

- [x] Stable lookup and launch idempotency live in workflow owners, not Web endpoint code.
- [x] Existing persistent idempotency store is reused.
- [x] Changed-payload replay fails before launch.
- [x] Snapshot `snap-20260726032132-5a6a0c3e` has no blocking errors or new cycles.

## After SB06

- [x] Evidence model references other runtimes through typed primitive links.
- [x] Human authorization and agent activation remain separate.
- [x] CRM-HR remains an optional projection/consumer, not a cyclic canonical owner.
- [x] Snapshot `snap-20260726043515-7a05e048` has no blocking errors, no findings on the
  new service/validator, and no new cycles.

## Before SB07/SB08

- Rerun scoped CodeAnalytics after source changes.
- Run `csharp-architecture-review-gate`.
- OpenAPI publication is blocked until all earlier architecture and behavior gates pass.
