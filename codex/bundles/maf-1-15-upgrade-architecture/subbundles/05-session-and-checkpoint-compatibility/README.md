# SB05 — Session and Checkpoint Compatibility

## Status

- `Ready after A2; closes with SB04`

## Objective

Prove and harden cross-version chat-session, provider-conversation, governed-step, attachment-scrub, background-response, and native workflow checkpoint behavior.

## Success Criteria

- Every relevant 1.13 session fixture has an explicit 1.15 outcome.
- Native 1.15 sessions round-trip.
- Provider-managed IDs are preserved without duplicate replay.
- Governed process isolation remains unchanged.
- Attachment payloads are removed while arbitrary state and approval binding survive.
- Serialization timeout/error categories are observable.
- Native workflow external request/checkpoint resumes after version change if the path is active.
- 1.15-to-1.13 rollback behavior is documented.
- A3 passes jointly with SB04.

## Covered Requirements

- R06, R12, R18, R19, R20, R22

## Prerequisites

- A2 GO;
- 1.13 session/checkpoint fixtures;
- actual checkpoint bridge and scrubber located.

## Exact Source References

- `MafRuntimeSessionBuilder.cs`
- `MafRuntimeSessionPersistenceDriver.cs`
- session compatibility model/store
- attachment scrubber
- provider history providers
- checkpoint bridge and stores
- governed process executor paths
- background response polling

## Deliverables

- cross-version session test suite;
- typed persistence diagnostics;
- scrubber state-preservation tests;
- checkpoint compatibility result;
- rollback compatibility result;
- dead replay hook decision;
- `proof/SB05/session-compatibility.md`;
- joint `proof/SB05/a3-decision.md`.

## Implementation Steps

1. Deserialize each sanitized 1.13 fixture under 1.15.
2. Classify success, transcript fallback, provider-managed continuation, reissue, or typed incompatibility.
3. Round-trip native 1.15 sessions.
4. Test strict JSON options and omitted null properties.
5. Preserve provider conversation IDs and prevent transcript duplication.
6. Preserve governed-step isolation and approval-continuation exception.
7. Test attachment scrub with arbitrary state-bag and binding entries.
8. Replace silent catch-all persistence outcomes with structured classification and logging.
9. Keep timeout bounded and test cancellation separately.
10. Trace native workflow checkpoint/external request usage.
11. Resume a 1.13 native fixture under 1.15 if applicable.
12. Remove compatibility code only after fixture proof.
13. Test 1.15 state under 1.13 or record required backup rollback.
14. Remove or deliberately implement the always-false approval replay hook.
15. Complete A3 review with SB04.

## Do Not Do

- do not replay transcript into provider-managed history blindly;
- do not remove governed isolation;
- do not persist request-scoped attachment bytes;
- do not swallow all failures without telemetry;
- do not edit MAF JSON keys in place;
- do not claim workflow checkpoint fix applies without a native fixture.

## Acceptance Checklist

- [ ] 1.13 fixture outcomes explicit
- [ ] native 1.15 round-trip
- [ ] provider ID preserved
- [ ] no duplicate replay
- [ ] governed isolation preserved
- [ ] scrub removes payload and retains state
- [ ] typed persistence diagnostics
- [ ] checkpoint result
- [ ] rollback result
- [ ] dead hook resolved
- [ ] A3 GO with SB04

## Proof Tier

- `Governed`

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

A3 requires SB04 and SB05 complete.

## Reopen Triggers

- session schema/store changes;
- provider changes conversation semantics;
- attachment content shape changes;
- workflow port/request types change;
- rollback target changes.

## Suggested Agent Prompt

```text
Implement SB05 only. Prove 1.13-to-1.15 session and native checkpoint behavior, preserve provider history and governed isolation, ensure attachment scrubbing retains approval state, add typed persistence diagnostics, test rollback, and remove compatibility code only after fixture proof.
```
