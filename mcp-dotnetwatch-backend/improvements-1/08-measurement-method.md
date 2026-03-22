# Measurement Method

## Objective
Quantify how much agent-facing log reduction decreases context consumption.

## Data sources
1. A noisy app log sample from `CanDoItAll`.
2. A warning-heavy app log or build log sample from `pveinvoicing`.
3. A representative operation log sample from `CanDoItAll`.

## Context assumptions
Use large-context planning windows that are realistic for current coding agents:
- 128k input tokens
- 200k input tokens

For relative credit analysis, assume any token-priced model scales linearly with input tokens. Report exact token savings and derive credit savings as the same percentage unless a concrete billing schedule is supplied separately.

## Metrics
For both raw and reduced outputs, record:
- entry count
- character count
- estimated token count using `chars / 4`

## Derived estimates
1. Savings ratio
   - `1 - reducedTokens / rawTokens`
2. Context-cycle estimate
   - estimate how many raw versus reduced build/start log payloads fit into a large agent context window
3. Relative processing savings
   - describe the approximate reduction in useless context ingestion and expected compression pressure

## Reporting rules
1. State clearly that token and credit figures are estimates.
2. Base estimates on real captured logs from this repo state.
3. Separate:
   - measured numbers
   - inferred impact
4. When a browser does not immediately reflect a static CSS change, record whether the server response changed and whether cache busting was required to observe the new asset.
