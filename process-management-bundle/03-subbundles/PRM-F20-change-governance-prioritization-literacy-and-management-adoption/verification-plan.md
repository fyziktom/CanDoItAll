# Verification plan — PRM-F20

## Expected verification outcomes

- Change proposals capture reason, impacted processes and roles, expected outcomes, risk, and rollout plan.
- Publish, retire, and critical-change operations can require governance approval based on criticality and impact.
- Affected owners, stewards, approvers, and participants receive communication and acknowledgement tasks when governed versions change.
- The process portfolio can classify criticality and prioritization tiers so not every process is modeled to the same depth.
- UI surfaces provide role-based guidance and glossary/help so middle management and operators can understand the process model.

## Automated tests

- Unit tests for new invariants and validation rules
- Integration tests for persistence and cross-module seams
- Component tests for editor or viewer surfaces where applicable
- Playwright coverage for the main happy path if new end-user flow is introduced

## Manual verification checklist

1. Submit a process change request and walk it through review and approval.
2. Publish a significant change and verify communications/acknowledgements are created.
3. Confirm prioritization tiers change the governance depth.

## Regression concerns to watch

- Versioning used without communication or impact analysis
- All processes forced into the same governance depth