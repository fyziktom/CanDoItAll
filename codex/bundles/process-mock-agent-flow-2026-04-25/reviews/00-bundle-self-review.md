# Bundle Self Review

## Preparation Review

- The bundle is intentionally split around the runtime seam rather than by UI or broad process refactor.
- The existing Scenario Harness is prior art, but it is not sufficient because the requested feature needs role-specific agents and settings-gated process-tuning behavior.
- The process dispatcher already has the outcome and artifact projection contracts needed for this test; adding mock behavior there would weaken the proof.

## Open Questions

- The exact process-definition fixture for the repair loop depends on existing test helpers and may need to be adapted during implementation.
- If a full process integration test is too expensive, closure must state the targeted replacement proof and the remaining gap.
