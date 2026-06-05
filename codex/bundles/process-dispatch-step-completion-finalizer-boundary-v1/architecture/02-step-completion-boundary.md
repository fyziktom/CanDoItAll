# Step Completion Boundary

## Stable orchestration responsibilities

Keep these in dispatcher-owned finalization flow:

- final transition through `TransitionStepWithClaimAsync`,
- dispatch claim verification,
- manager artifact recovery invocation,
- post-recovery revalidation ordering,
- cost synchronization after successful transition,
- DB mutation/persistence coordination.

## Extractable responsibilities

These may move into module-local helpers:

- finalizer type/value definitions,
- artifact content read result and readers,
- artifact validation context creation,
- transition request construction from finalizer result,
- runtime invariant violation construction,
- artifact validation diagnostic payload construction,
- pure line/fingerprint/summary helpers.

## Proof principles

Every extraction must prove:

- same statuses,
- same block cause behavior,
- same selected branch outcome handling,
- same artifact validation context fields,
- same runtime invariant blocking behavior,
- same manager recovery revalidation behavior,
- same no-transition behavior for stale/in-progress cases.
