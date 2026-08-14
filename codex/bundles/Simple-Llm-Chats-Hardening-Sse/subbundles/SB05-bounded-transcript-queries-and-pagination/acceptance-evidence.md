# Acceptance evidence — SB05

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Transcript paging executes a bounded SQL query and never materializes the full transcript.
- [ ] Conversation and definition listings do not issue one query per item.
- [ ] Context-window construction reads only the bounded entries it can send.
- [ ] Externally exposed collections use deterministic cursors and enforced page limits.
- [ ] Large-transcript tests prove stable memory/query behavior without changing canonical content.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
