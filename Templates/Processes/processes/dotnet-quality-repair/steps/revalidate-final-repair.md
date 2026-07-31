# Independently validate the final repair

Independently reproduce every unresolved required ProductAcceptance criterion and satisfy the current-execution validation gates defined by this step. The exact terminal branch keys are `quality-repair-accepted` and `quality-repair-no-go`.

Select acceptance only when criterion-specific proof is complete and no release blocker remains. Select no-go when executed proof still fails, the final action is incomplete, or a new release blocker exists. Negative proof is a completed no-go decision, not a Blocked outcome.

This step is read-only. It must not mutate the product or start another repair.
