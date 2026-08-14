# Acceptance evidence — SB09

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Turn start returns 202 without waiting for provider completion.
- [ ] SSE delivers ordered deltas and exactly one terminal operation event.
- [ ] Reconnect resumes without duplicate semantic text or a second provider call.
- [ ] A replay gap emits stream.gap with a usable recovery cursor while status remains authoritative.
- [ ] SSE disconnect does not cancel or abandon the operation.
- [ ] Explicit cancellation is visible in operation status and event stream.
- [ ] The stream closes after terminal success, failure, cancellation, or RecoveryRequired.
- [ ] Existing anti-buffering, heartbeat, cursor, and profile-lifetime behavior is reused.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
