# Capture post-release learning and corrective actions

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `post-release-learning`  
**Step kind:** End  
**Target lead hours:** 6

## Summary
Forensic replay and systemic follow-up

## Notes
Turn the release outcome into explicit learning about design, QA, operations, and process behavior.

## Contracts
- Input contract: Rollout outcome, telemetry record, support observations, and any release incident notes.
- Output contract: Post-release learning review with corrective actions and simulation updates.
- Evidence contract: Timeline, contributing factors, missing controls, and next corrective actions.

## Governance
- Decision rights: Delivery manager owns follow-up assignment while architecture and release roles retain responsibility for their own control gaps.
- Exception policy: Do not close the process while critical corrective actions remain unnamed or unowned.
- Requires approval: False
- Requires decision record: True

## Dependencies
- execute-release-rollout

## Role assignments
- `delivery-manager` / Delivery manager => Responsible; required=True; fallback-order=0; rebind=Follow-up ownership remains explicit until corrective actions have accountable owners.
- `solution-architect` / Solution architect => Reviewer; required=True; fallback-order=0; rebind=Architecture role reviews systemic design findings rather than only local defects.
- `release-manager` / Release manager => Reviewer; required=True; fallback-order=0; rebind=Release role reviews telemetry and rollback-control lessons.

## Artifact expectations
- `post-release-learning-log` -> `retrospective-improvement-log` / Post-release learning review | kind=Decision | trust=ReviewRequired | sensitivity=Internal | validation=Must capture orchestration-quality observations, not only technical defects.

## Artifact inputs
- No explicit artifact inputs.

## Branch outcomes
- No explicit branch outcomes.
