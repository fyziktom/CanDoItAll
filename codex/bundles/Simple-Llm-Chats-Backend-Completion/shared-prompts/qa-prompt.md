# Shared QA Prompt

```text
Gate the named subbundle as Pass, Fail, or Blocked using current source and portable proof.

Verify:
- prerequisites and start/end commits;
- exact requirement ownership and no UI/scope expansion;
- every changed production project built;
- filter, expected/actual discovery, and exact focused result;
- realistic positive and meaningful negative evidence;
- canonical PostgreSQL/API/SSE/log outcomes and secret allowlists where applicable;
- dependency direction, lifetime ownership, testability, partial-class policy, and anti-stub checks;
- invalidation keys and downstream work that must reopen;
- Governed manifest hashes/transcripts/invariants/review when declared;
- raw-input and execution-ledger updates.

Fail missing or contradictory evidence that is repairable. Mark Blocked only for a real external-state/authority dependency. Never convert missing proof, CI, PostgreSQL, pending-model, or discovery evidence into a residual risk or status-only pass.
```
