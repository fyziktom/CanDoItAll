# Release Decision

## Decision

`Not merge-ready`.

## Basis

The deterministic stabilization matrix is green:

- Build completed with 0 warnings and 0 errors: `bundle://proof/SB06/transcripts/build.txt`.
- Full unit suite passed on clean rerun, 1142/1142: `bundle://proof/SB06/transcripts/unit-tests-rerun.txt`.
- Focused process runtime integration matrix passed, 21/21: `bundle://proof/SB06/transcripts/focused-integration-matrix.txt`.
- Large-desktop Playwright launch-to-completed-run proof passed, 1/1: `bundle://proof/SB06/transcripts/focused-playwright-final.txt`.
- Live OpenAI settings guards passed, 7/7: `bundle://proof/SB06/transcripts/live-openai-settings-tests.txt`.

The first unit-suite run had one cleanup-only PostgreSQL permission failure while disposing a test database lease:
`AppDbContextRuntimeSwitchTests.CreateDbContextAsync_keeps_canonical_profile_until_restart_after_activation`.
The failed test passed when rerun alone, and the full unit suite passed on a clean rerun.

## Blockers

The code-first closure gate fails:

- `SourceAndTestChangedLines: 652`
- `BundleChangedLines: 3668`
- `RequiredSourceAndTestLinesAt5xBundle: 18340`
- `RatioPass: False`

Transcript: `bundle://proof/SB06/transcripts/code-first-ratio.txt`.

Live OpenAI smoke is honestly skipped and not counted as deterministic release proof. `OPENAI_API_KEY` is present, but the explicit opt-in/model/timeout/token-budget variables are absent:

- `CANDOITALL_RUN_LIVE_PROCESS_RUN_VALIDATION`
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`
- `CANDOITALL_LIVE_PROCESS_RUN_OPENAI_MODEL`
- `CANDOITALL_LIVE_PROCESS_RUN_TIMEOUT_SECONDS`
- `CANDOITALL_LIVE_PROCESS_RUN_MAX_TOTAL_TOKENS`

Transcript: `bundle://proof/SB06/transcripts/live-openai-classification.txt`.

## Merge Path

Do not add code solely to satisfy the ratio. The implementation proof should remain honest.

To make this branch merge-ready, either:

- handle the bundle-proof churn outside the code-first gate policy, if that policy is intended to evaluate source/test implementation rather than proof transcripts; or
- add real source/test work only if another runtime gap is found by review or testing.

Until that policy decision is made, this bundle closes with deterministic runtime stabilization proof green and the final release decision `not merge-ready`.
