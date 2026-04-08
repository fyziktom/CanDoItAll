# Finding 02: Legacy `Behavior` focused-context intent alias still fails

## Trigger

- Historical Zyphonote scenario 4 troubleshooting
- Installed server test after the parity work

## Observation

`code_analytics_focused_context_get` succeeds for the explicit member seed when the caller uses:

- `intent = TroublePath`
- `precision = Balanced`

The same call still fails generically when the caller sends `intent = Behavior`, which older helper prompts and stale tool descriptions have used as a synonym.

## Why this matters

The core tool is now capable of answering the scenario, but stale clients can still hit a confusing invocation failure instead of a deterministic validation error or alias mapping.

## Evidence

- `Behavior` request returned a generic MCP invocation failure
- `TroublePath` request returned a valid focused-context response with the explicit `ApplyExternalScoreAsync` seed and code excerpts

## Improvement options

- Add host-side alias mapping from `Behavior` to `TroublePath`
- Or ensure every client refreshes its generated MCP schema after reinstall
- Keep the repo skill on the deterministic `symbols_search` -> `symbol_definition_get` path for exact method behavior questions
