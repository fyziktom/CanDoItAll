# Session handoff — SB08

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

- [ ] Every operation event has a unique monotonic sequence within its operation.
- [ ] State-transition events commit in the same transaction as their state.
- [ ] Text chunks are coalesced and bounded rather than one row per token.
- [ ] Partial output is replayable but never canonical unless finalization succeeds.
- [ ] A second instance reads all committed events without first-instance memory.
- [ ] Event payloads contain no system prompt, user prompt, credential, or raw provider error.

## Architecture result

- [ ] Owner moved or strengthened as planned
- [ ] Old shallow path removed/unreachable
- [ ] Direct tests target the new owner
- [ ] No forbidden reference/cycle/partial expansion
- [ ] Architecture record updated if design changed

## Progression

Pending. Use `Ready`, `Blocked`, or `Reopened`; explain downstream impact.
