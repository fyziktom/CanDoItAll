# Verification plan — PRM-F07

## Expected verification outcomes

- Runs follow only valid state transitions.
- Conflicting claims and double completions are rejected.
- Assignment resolution respects pool/eligibility/capacity logic.
- Rebind and fallback events are journaled.

## Automated tests

- Unit tests for state-machine and assignment invariants
- Integration tests for persistence, concurrency, and rebind behavior
- Component tests for any run/assignment UI changes
- Playwright tests when the feature changes critical runtime flows

## Manual verification checklist

1. Start a run from a published definition.
2. Exercise claim / complete / block / resume paths.
3. Exercise at least one capacity or validation mismatch path.
4. Exercise at least one fallback or rebind path.
5. Confirm journal side effects and persisted state.

## Regression concerns to watch

- Mutable published-definition behavior
- Assignment rules that ignore validation/capacity
- Rebinds that are not auditable
- SQLite-only assumptions that break PostgreSQL or vice versa
