# SB03 Tetris Cost Reconciliation

## Captured Old Evidence

Input fixture:

`codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/agent-execution-runs-for-process-6724.json`

The captured manager answer for process run `6724b4c8-c774-4880-becc-940a3d7bf155` reported:

- Estimated token usage: `5,360`
- Actual cost: `0.082678`
- No separate actual-token field in the run detail

The run detail embedded in the captured session state also contains `estimatedCost=5360` and `actualCost=0.082678`, but no provider-response usage record. That is the old failure mode: the application could answer with a precise-looking cost while admitting it had no explicit actual tokens consumed.

## New Behavior

SB03 changes future run accounting from metric-only aggregation to provider usage observations:

- Provider usage is captured at runtime response, finalizer, failure-after-provider-call, structured-output repair, workflow LLM, and legacy metric bridge boundaries.
- Known actual cost is summed from known usage observations only.
- Missing or unavailable provider usage is represented as `MissingAfterProviderActivity`, `UsageUnavailable`, or `EstimatedFromMetric` instead of silently becoming zero or a known actual total.
- Process cost synchronization uses the usage ledger first and avoids double-counting legacy metrics when ledger observations exist.

## Reconciliation Result

For the historical Tetris capture, provider billing export/API data is not available locally, and the old API capture does not include response-level provider usage. External OpenAI dashboard reconciliation therefore remains pending external verification as allowed by the SB03 scope exception.

The internal reconciliation is complete:

- Old metric-derived view: `estimatedCost=5360`, `actualCost=0.082678`, no actual-token evidence.
- New ledger-capable view for future equivalent runs: observed provider usage is persisted with source phase and provider response identity when available; if not available after provider activity, the run records an explicit unknown/estimated observation and the UI/API can report that known actual token totals are incomplete.
- Regression proof: the failing-first mutation shows the old undercount class still fails the SB03 tests, while the restored implementation passes the finalizer, failure, repair, workflow, and process-cost ledger slices.
