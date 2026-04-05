# Required tests

At minimum add or update tests that prove:

- the problem described in `P7-005` is fixed
- the fix remains stable across future refactors
- negative cases fail loudly

## Suggested proof style

- one integration or architecture guardrail test for the happy path
- one guardrail/negative test proving the forbidden pattern cannot come back silently
