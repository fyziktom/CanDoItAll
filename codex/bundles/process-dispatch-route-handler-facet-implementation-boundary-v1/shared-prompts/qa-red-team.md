# QA / Red-team Prompt

Review the implementation as a behavior-preserving route-handler refactor.

Attack these failure modes:
- route order changed,
- handler returns `DispatchComplete` instead of `NotHandled`,
- handler returns `ContinueCandidates` incorrectly,
- claim lost paths changed,
- heartbeat lost paths changed,
- generic failure transition changed,
- finalizer handoff changed,
- subprocess terminal/completion behavior changed,
- workflow handled/not-handled behavior changed,
- direct-agent competing execution guard changed,
- Process Core or driver API introduced,
- UI proof or viewport artifacts created,
- execution report collapsed rows.

Do not accept build-only proof.
