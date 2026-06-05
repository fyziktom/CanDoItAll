# QA / Red-Team Prompt

Review the implementation for behavior drift, shallow extraction, hidden side effects, and forbidden boundary expansion.

Reject if:

- Process Core or production driver APIs appear.
- Retry/provider/no-progress behavior changes without explicit test proof.
- Provider `SaveAgentAsync` side effects are hidden in pure rules.
- No-progress fingerprint, event type, correlation id, replay context, or retry stop/continue behavior changes.
- Browser/mobile/small/medium screenshots are produced for this runtime-only refactor.
- Subbundle proof is prose-only without source assertions and test/build evidence.
