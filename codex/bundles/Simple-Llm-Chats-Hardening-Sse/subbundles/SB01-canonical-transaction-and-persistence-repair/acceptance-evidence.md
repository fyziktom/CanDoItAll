# Acceptance evidence — SB01

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Conversation title and transcript metadata have exactly one canonical writable owner.
- [ ] Conversation creation commits product binding and transcript root together or commits neither.
- [ ] Conversation rename updates the canonical title once and cannot leave divergent rows.
- [ ] No production conversation store creates a second AppDbContext inside an active product command.
- [ ] Migration and transfer payloads preserve the repaired canonical model.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
