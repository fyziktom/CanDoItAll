# Assumptions And Risks

## Assumptions

- Some failures are obsolete tests after intentional repository layout/template changes.
- Some failures are real production guard regressions, especially branch-signal recovery and watch restore stale-reference behavior.
- EF model state is currently clean because `dotnet ef migrations has-pending-model-changes` reports no changes.
- The full unit suite may still contain unrelated failures after these clusters are repaired; those must be recorded precisely rather than hidden.

## Critical Path Risks

- Weakening hygiene tests could let transient Codex bundle outputs or work-package identifiers leak into tracked source.
- Updating process-template tests to match current prose without preserving behavior invariants could remove an important runtime/process guard.
- Adding an EF migration for an order-dependent static-state issue would create churn and still leave the suite flaky.
- Starting `5032` before fixing build/test locks can leave stale app processes holding output DLLs and hiding real rebuild failures.

## Validation Risks

- Broad `dotnet test` runs can hang or become noisy; targeted failing-first proof must be captured before repairs.
- `AppDbContextRuntimeSwitchTests` passes in isolation, so reproducing the historical EF failure may require order-specific or full-suite proof.
- `dotnet ef` uses global `dotnet-ef` 10.0.3 against runtime 10.0.4; this is a warning today but should be tracked.
- Browser smoke proof must verify the app process that was just rebuilt, not an old process already serving port `5032`.

## Reopen Triggers

- A repaired hygiene test passes only because the scanner was disabled or given a broad path exemption.
- EF pending-model check fails after test-isolation fixes.
- Full unit suite still hangs without a captured blame/hang artifact.
- `5032` serves an old build, returns non-success, or browser/API smoke cannot reach the app.
- Any branch-signal repair only satisfies the current fixture and does not prove explicit branch outcome lines, heading-plus-next-line, and title inference semantics.
