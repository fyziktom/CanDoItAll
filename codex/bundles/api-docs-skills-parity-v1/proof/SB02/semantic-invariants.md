# SB02 Semantic Invariants

- Invariant ID: SB02-INV-001
- Source raw note: API surface coverage must be repaired where APIs were out of date.
- Expected behavior: Focused OpenAPI coverage asserts the missing Cognitive Memory contract, projection rebuild, automation run, retention cleanup, and v1 alias routes.
- Disallowed shallow implementation: Updating docs or skills without making the API contract test fail on missing route exposure.
- Failing-first test: N/A, non-production test assertion repair; the added assertions are the guard against future missing routes.
- Passing test: `bundle://proof/SB02/transcripts/api-openapi-route-test.md`
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ApiIntegrationTests.cs`
- Production assertions: No runtime route implementation changed; existing Minimal API routes are asserted through OpenAPI.
- Red-team negative case: `bundle://proof/SB02/transcripts/anti-stub-audit.md`
- Downstream dependency check: SB04 and SB05 used this route proof before documenting Cognitive Memory v1 and operations.

