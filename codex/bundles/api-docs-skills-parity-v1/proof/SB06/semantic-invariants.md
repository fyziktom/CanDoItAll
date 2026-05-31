# SB06 Semantic Invariants

- Invariant ID: SB06-INV-001
- Source raw note: The repo needs a way to avoid getting lost and letting docs/skills drift again.
- Expected behavior: A focused unit guardrail fails when high-risk route/docs/skills coverage is removed.
- Disallowed shallow implementation: A test that only checks files exist or generated output is non-empty.
- Failing-first test: N/A, non-production guardrail addition; the test source names the high-risk coverage it protects.
- Passing test: `bundle://proof/SB06/transcripts/api-docs-skills-parity-test.md`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ApiDocsSkillsParityTests.cs`
- Production assertions: No runtime production behavior changed in SB06.
- Red-team negative case: `bundle://proof/SB06/transcripts/anti-stub-audit.md`
- Downstream dependency check: SB07 final closure used the passing guardrail plus the focused OpenAPI route test.

