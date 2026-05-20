# Assumptions And Risks

## Assumptions

- The existing seeded default agents represent the complete default catalog to migrate.
- A file pack made of `manifest.json`, per-team `team.json`, and per-agent markdown/JSON files is simple enough for template tuning.
- Default seed tests can prove data-shape preservation before browser validation checks the app surface.

## Critical Path Risks

- SB01 is critical because an incorrect loader or template shape would make every later seed result untrustworthy.
- SB02 is critical because stale hardcoded definitions or missing team merge logic would leave the new templates decorative instead of authoritative.
- SB03 is critical because tests alone cannot prove the app still displays and uses the seeded agents correctly.

## Validation Risks

- Browser validation may be blocked by local service setup or database state; if so, the execution report must name the exact blocker and preserve command proof.
- Test filters can miss cross-cutting seed regressions; include seed/runtime integration tests rather than only loader unit checks.
- Source audits can produce false positives because non-default test helpers intentionally construct `AgentDefinition`.

## Reopen Triggers

- Reopen SB01 if any template folder lacks instructions, settings, skills, provider key, or capability mapping.
- Reopen SB02 if hardcoded default-agent instruction assets or default-agent literal blocks remain in production seed code.
- Reopen SB03 if targeted tests fail, the browser route cannot load, or expected seeded teams/agents are absent from the UI.
