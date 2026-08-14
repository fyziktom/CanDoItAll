# Acceptance evidence — SB02

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Turn admission is one transaction across operation, transcript, evidence, and event state.
- [ ] Successful completion is one transaction across assistant message, usage, active-turn clearing, and operation success.
- [ ] Failed compensation cannot leave a terminal Failed or Cancelled operation with a live active turn.
- [ ] A cancellation request committed before finalization prevents Succeeded.
- [ ] Same operation ID and fingerprint replays the original result even after later lifecycle changes.
- [ ] Same operation ID with a different fingerprint conflicts before provider dispatch.
- [ ] Conversation archive cannot race an active or nonterminal turn.
- [ ] Direct completion and recovery reduce identical durable evidence to the same outcome.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
