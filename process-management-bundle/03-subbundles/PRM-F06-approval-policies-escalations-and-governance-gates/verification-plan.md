# Verification plan — PRM-F06

## Expected verification outcomes

- Approval-required steps pause correctly and resume on decision.
- Rejected approvals create an auditable branch or blocked state.
- Self-approval and conflicting-role combinations are rejected or forced through an explicit override path.

## Automated tests

- Unit tests for approval, escalation, and separation-of-duties invariants
- Integration tests for persistence and cross-module contracts
- Component tests for any new or changed Blazor surface
- Playwright tests when the feature changes critical navigation or full workflows

## Manual verification checklist

1. Start the app and open the affected process surfaces.
2. Exercise the smallest happy path that proves approvals work.
3. Exercise at least one self-approval or role-conflict edge path.
4. Confirm activity/journal/DB side effects where relevant.
5. Re-open the app or route to confirm persisted state behaves correctly.

## Regression concerns to watch

- Hidden canonical writes into Workbench metadata
- Governance rules stored only in runtime state
- SQLite-only assumptions that break PostgreSQL or vice versa
- Override paths that bypass auditing
