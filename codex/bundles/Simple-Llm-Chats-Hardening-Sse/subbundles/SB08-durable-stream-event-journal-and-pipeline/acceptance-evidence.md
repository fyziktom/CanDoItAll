# Acceptance evidence — SB08

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Every operation event has a unique monotonic sequence within its operation.
- [ ] State-transition events commit in the same transaction as their state.
- [ ] Text chunks are coalesced and bounded rather than one row per token.
- [ ] Partial output is replayable but never canonical unless finalization succeeds.
- [ ] A second instance reads all committed events without first-instance memory.
- [ ] Event payloads contain no system prompt, user prompt, credential, or raw provider error.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
