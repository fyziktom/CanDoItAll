# Execute repaired controlled release rollout

Deploy, publish, export, or hand off the approved deliverable inside the declared release boundary while rollback, removal, or recovery readiness remains explicit. Use live telemetry only when the boundary includes a live service or production host.

## Contract
- Inputs: Approved repaired release record, delivery package or artifact root, rollback or removal plan, declared release boundary, and applicable watch points.
- Outputs: Executed repaired rollout, publish, export, or handoff with explicit boundary outcome, rollback/removal status, and watch notes where applicable.
- Evidence: Operator notes, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable entries for out-of-boundary production controls, and any rollback, removal, or release halt.
- Operation target scope: `ExternalActionControlled`
