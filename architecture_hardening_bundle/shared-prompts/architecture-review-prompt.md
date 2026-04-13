# Architecture review prompt

Run this prompt at review gates A-D.

## Goal

Decide whether the implementation is going in the right architectural direction, not merely whether some tests passed.

## Mandatory review questions

1. Is there still any hidden second source of truth?
2. Did the new split improve responsibility clarity, or just move code around?
3. Is the compatibility boundary narrow and explicit?
4. Did the work reduce long-term maintenance risk?
5. Did the proof cover the risk that justified the phase?
6. Would continuing now make later proof misleading if something foundational is still wrong?

## Decision outcomes

- `Pass` — downstream work may continue.
- `Pass with explicit follow-up` — only if the gap is truly non-blocking and documented.
- `Fail` — create a corrective subbundle immediately and block downstream work.

## Recording rule

Every gate must produce:
- a memo in `reviews/02-architecture-gate-memo-log.md`,
- a status row in `reviews/01-execution-report.md`,
- a go/no-go decision.
