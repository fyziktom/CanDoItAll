# 11 Test Suite Stabilization and Behavior Gates

## Goal

Make tests trustworthy and aligned with current behavior.

## Tasks

1. Keep a default green gate with quarantined/live tests excluded.
2. Ensure focused gates for this bundle exist and run actual tests.
3. Replace brittle private-method reflection tests with public service behavior tests where possible.
4. Ensure Playwright/live-process tests are intentionally categorized.
5. Fix or quarantine obsolete tests with documented rationale.
6. Add tests that fail if `01-execution-report.md` claims missing files/tests.

## Acceptance criteria

- Default gate is green.
- No-filter full suite status is honestly reported.
- Bundle-specific behavior tests prove the implementation.
