# Normalized Requirements

## Requirements

- `REQ-01` The solution and project inventory surfaces must classify projects into at least product, test, and benchmark or supporting roles using first-class response data rather than caller name heuristics alone.
- `REQ-02` Product-architecture answers for `Zyphonote.MusicTheory.Core` must no longer mix `Zyphonote.MusicTheory.Tests` and `Zyphonote.MusicTheory.Benchmarks` into the primary reverse-reference answer path.
- `REQ-03` Supporting-project references must remain observable after the inventory precision fix so the MCP does not silently lose factual coverage.
- `REQ-04` `code_analytics_focused_context_get` must accept the historical `Behavior` intent alias and resolve it to the same behavior path as `TroublePath`.
- `REQ-05` The current deterministic symbol-first skill guidance must remain intact for exact method-behavior questions.
- `REQ-06` The updated MCP must build, reinstall, and rerun successfully against Zyphonote with evidence captured in this bundle.

## Closure Targets

- Close prior finding `finding-01-solution-inventory-mixes-product-and-test-projects.md`
- Close prior finding `finding-02-legacy-focused-context-behavior-intent-alias-fails.md`

## Non-Goals

- Do not redesign the entire focused-context selection strategy.
- Do not re-open SharpTools comparison work unless a regression forces it.
- Do not rebuild the snapshot domain model if response-level enrichment can close the gap safely.
