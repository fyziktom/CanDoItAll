# Verification plan — PRM-F04

## Expected verification outcomes

- Steps cannot be marked complete when mandatory evidence/contract conditions are unmet if policy requires it.
- Contract metadata survives publish and clone workflows.

## Automated tests

- Unit tests for core rules and invariants relevant to `PRM-F04`
- Integration tests for persistence and cross-module contracts
- Component tests for any new or changed Blazor surface
- Playwright tests when the feature changes critical navigation or full workflows

## Manual verification checklist

1. Start the app and open the affected process surfaces.
2. Exercise the smallest happy path that proves this feature works.
3. Exercise at least one invalid or edge path.
4. Confirm activity/journal/DB side effects where relevant.
5. Re-open the app or route to confirm persisted state behaves correctly.

## Regression concerns to watch

- Broken shell navigation
- Broken project navigation
- Hidden canonical writes into Workbench metadata
- SQLite-only assumptions that break PostgreSQL or vice versa
- UI state being mistaken for canonical runtime state
