# Full Unit Debt Policy

## Current issue
Previous proof indicates full unit now passes with 21 SB004-owned skips, representing stale historical architecture fixture paths.

## Required next action
- Replace skipped historical bundle-fixture tests with current source-backed architecture tests, or move them to a clearly named historical-fixture test class with fixture files restored.
- Full unit proof after this bundle should target 0 failures and either 0 skips or a strictly smaller, current-owned skip ledger.
- Any remaining skip must have:
  - exact test name,
  - owner,
  - reason,
  - reopen trigger,
  - replacement current-source guard.

## Hard rejection
Do not treat skipped architecture fixtures as permanent closure for stable Core/driver release readiness.
