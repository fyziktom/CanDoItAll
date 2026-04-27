# 08 — Test Suite Taxonomy and Default Green Gate


## Problem

The broad test suite is not green and appears to mix unit/component/integration/live-process/browser tests without a reliable default gate.

## Tasks

1. Introduce consistent xUnit categories/traits.
2. Decide whether the no-filter full solution command must pass, or define a documented default filtered gate.
3. Move genuinely heavy/live/browser tests behind explicit categories if needed.
4. Do not hide obsolete failures silently. Delete obsolete tests with rationale or mark as `Quarantined` with issue references and replacement coverage.
5. Add a `docs/testing.md` or update existing docs with commands.
6. Ensure CI/verification commands match docs.

## Acceptance criteria

- A default green gate is documented and passes.
- Extended gates are documented and runnable.
- Quarantined tests have rationale and owner/next action.
- Execution report distinguishes targeted, default, full, and extended test outcomes.

