# Session handoff — SB04

State: **Locked**

## Entry checklist

- [ ] Root bundle status read
- [ ] Dependencies complete and proof trusted
- [ ] Actual repository/branch/head recorded
- [ ] Current source and nearby tests inspected
- [ ] Test budget understood
- [ ] Database/dependency mode recorded

## Work performed

Pending.

## Files changed

Pending.

## Commands and results

Pending. Include exact command, exit code, passed/failed/skipped counts and evidence path.

## Bugs discovered and resolved

Pending.

## Deviations

Pending. `None` is acceptable only after review.

## Acceptance result

- [ ] Only one instance can hold an execution lease for an operation at a time.
- [ ] A client disconnect after admission does not cancel the durable operation.
- [ ] Explicit cancellation reaches a local owner and is observed cross-instance within the configured bound.
- [ ] Local registry absence never recovers or abandons another instance's live operation.
- [ ] Expired pre-dispatch work may be reclaimed, while expired post-dispatch work becomes RecoveryRequired.
- [ ] A host without an available dispatcher cannot falsely accept unexecutable work.

## Architecture result

- [ ] Owner moved or strengthened as planned
- [ ] Old shallow path removed/unreachable
- [ ] Direct tests target the new owner
- [ ] No forbidden reference/cycle/partial expansion
- [ ] Architecture record updated if design changed

## Progression

Pending. Use `Ready`, `Blocked`, or `Reopened`; explain downstream impact.
