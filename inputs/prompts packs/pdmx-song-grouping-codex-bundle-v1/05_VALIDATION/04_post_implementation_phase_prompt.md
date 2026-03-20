# Post-Implementation Phase Prompt

Use this only if the validator concludes that follow-up implementation work is needed.

```text
You are continuing the PDMX song-grouping implementation after validator review.

Read:
- the validator findings,
- the current repository diff,
- the original grouping bundle.

Your objective is to complete a focused remediation phase, not to rewrite the subsystem.

Rules:
- preserve already-correct work,
- fix only the validator-confirmed gaps,
- do not mutate the original real DB,
- keep comments in English,
- update tests for every fixed issue.

Required output:
1. list the validator issues you are fixing,
2. implement only those fixes,
3. add or adjust tests,
4. summarize residual risks,
5. provide a short handoff note for re-validation.

Common remediation buckets:
- normalization misses
- false positives from scoring
- manual lock overwrite bugs
- apply-flow idempotency bugs
- missing review UI affordances
- copied-DB validation gaps
```
