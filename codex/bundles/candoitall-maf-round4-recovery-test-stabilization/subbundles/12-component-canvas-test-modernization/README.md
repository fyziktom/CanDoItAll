# 12 — Component and Canvas Test Modernization


## Problem

Some component/canvas assertions are likely obsolete or brittle. They may assert implementation details instead of behavior.

## Tasks

1. Identify failing component/canvas tests from the broad suite.
2. For each failure, classify:
   - real product regression;
   - obsolete expectation;
   - brittle markup/CSS assertion;
   - browser/layout behavior that belongs in Playwright.
3. Update tests to semantic assertions, stable selectors, or accessibility checks.
4. Move browser-only behavior to Playwright where appropriate.
5. Delete obsolete tests only with documented rationale and replacement coverage.

## Acceptance criteria

- Component tests assert stable behavior, not incidental markup.
- Obsolete tests are removed or quarantined with rationale.
- Canvas/browser-specific behavior has proper browser-level coverage or is documented as out of scope.

