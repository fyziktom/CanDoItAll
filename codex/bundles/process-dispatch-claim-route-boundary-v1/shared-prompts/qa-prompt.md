# QA Prompt

Review the implementation against these questions:

1. Did the subbundle preserve existing dispatch behavior?
2. Are route helpers pure decision helpers unless explicitly named as coordinators?
3. Did claim/heartbeat handling preserve lost-claim behavior?
4. Did concurrency selection preserve stale/recoverable/current-attempt semantics?
5. Did any code introduce Process Core or production driver APIs?
6. Did any code broaden MAF/Tooling product dependencies?
7. Did any UI file change? If yes, stop and require scope review.
8. Are small/medium/mobile proof artifacts absent?
9. Are line counts moving in the intended direction without creating a new monolith?
10. Is every downstream subbundle still trustworthy after this change?
