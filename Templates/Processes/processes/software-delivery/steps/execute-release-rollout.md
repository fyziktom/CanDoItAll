# Execute controlled release rollout or handoff

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `execute-release-rollout`  
**Step kind:** Delivery  
**Target lead hours:** 4

## Summary
Boundary-scoped release execution and watch

## Notes
Deploy, publish, export, or hand off the approved deliverable inside the declared release boundary while rollback, removal, or recovery readiness remains explicit. Use live telemetry only when the boundary includes a live service or production host.

## Contracts
- Input contract: Approved release record, delivery package or artifact root, rollback or removal plan, declared release boundary, and applicable watch points.
- Output contract: Executed rollout, publish, export, or handoff with explicit boundary outcome, rollback/removal status, and watch notes where applicable.
- Evidence contract: Operator notes, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable entries for out-of-boundary production controls, and any rollback, removal, or release halt.

## Governance
- Decision rights: Release manager may execute only inside the approved boundary, window, and rollback-trigger limits.
- Exception policy: Trigger halt, rollback, removal, or no-go immediately when applicable telemetry, artifact integrity, user impact, data impact, or operational constraints breach the approved threshold. Do not block for missing public deployment, CI, or production telemetry when the approved boundary is a package or output-folder handoff.
- Requires approval: False
- Requires decision record: False

## Dependencies
- release-approval

## Role assignments
- `release-manager` / Release manager => Responsible; required=True; fallback-order=0; rebind=Execution ownership remains with the release-manager role until rollout completes or fails.
- `lead-engineer` / Lead engineer => Backup; required=False; fallback-order=1; rebind=Implementation owner stays available for rapid corrective interpretation during rollout.
- `delivery-manager` / Delivery manager => Reviewer; required=True; fallback-order=0; rebind=Governance owner reviews telemetry and escalation timing during the release window.

## Artifact expectations
- `deployment-watch-log` -> `release-readiness-report` / Deployment, handoff, and watch log | kind=Transcript | trust=ReviewRequired | sensitivity=Internal | validation=Must capture release timing, artifact placement or deployment receipt, applicable telemetry or smoke checkpoints, not-applicable out-of-boundary controls, and any halt, rollback, or removal decision.

## Artifact inputs
- From step `release-approval` expectation `release-approval-record`

## Branch outcomes
- No explicit branch outcomes.
