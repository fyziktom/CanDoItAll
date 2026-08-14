# Acceptance evidence — SB04

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Only one instance can hold an execution lease for an operation at a time.
- [ ] A client disconnect after admission does not cancel the durable operation.
- [ ] Explicit cancellation reaches a local owner and is observed cross-instance within the configured bound.
- [ ] Local registry absence never recovers or abandons another instance's live operation.
- [ ] Expired pre-dispatch work may be reclaimed, while expired post-dispatch work becomes RecoveryRequired.
- [ ] A host without an available dispatcher cannot falsely accept unexecutable work.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
