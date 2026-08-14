# Acceptance evidence — SB03

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Every public LLM Chat application operation captures profile identity before its first read.
- [ ] All repositories, provider resolution, transcript commands, and audit writes use the captured operation scope.
- [ ] A profile switch prevents every subsequent old-generation durable commit.
- [ ] A switch during provider execution yields deterministic non-success or RecoveryRequired with retained usage evidence.
- [ ] No current-profile DbContext or provider lease is cached across operations.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
