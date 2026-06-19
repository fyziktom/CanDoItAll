# Execute repaired controlled release rollout

Deploy, publish, export, or hand off the approved deliverable inside the declared release boundary while rollback, removal, or recovery readiness remains explicit. Use live telemetry only when the boundary includes a live service or production host.

Before returning Completed, write the repaired rollout record to `artifacts/process-runs/<current-process-run-id>/steps/execute-release-rollout-after-repair.md` and include that exact path in `evidenceRefs`. For a local generated application or output-folder handoff, record the validated artifact root, repaired run command references, rollback/removal trigger, and the reason production telemetry or deployment-window controls are not applicable.

## Contract
- Inputs: Approved repaired release record, delivery package or artifact root, rollback or removal plan, declared release boundary, and applicable watch points.
- Outputs: Executed repaired rollout, publish, export, or handoff with explicit boundary outcome, rollback/removal status, and watch notes where applicable.
- Evidence: Repaired rollout record at `artifacts/process-runs/<current-process-run-id>/steps/execute-release-rollout-after-repair.md`, operator notes, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable entries for out-of-boundary production controls, and any rollback, removal, or release halt.
- Operation target scope: `ExternalActionControlled`
