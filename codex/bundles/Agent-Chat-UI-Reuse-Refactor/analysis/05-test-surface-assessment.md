# Test-surface assessment

## Known owner-test candidates

These are orientation anchors, not hard-coded authoritative selectors:

- `ChatWorkspacePanelTests`
- `AgentChatPanelResponsivenessTests`
- `AgentChatModalTests`
- `AgentCompactListTests`
- `AgentCatalogPanelTests`
- `AgentChatContextSurfaceProviderTests`
- `AgentDetailsDialog*Tests`
- floating Agent Chat component tests discovered from the live test project
- Process workspace component tests that consume Agent Chat surfaces
- contextual Agent workspace tests
- focused Playwright scenarios for Agents and floating chats

The actual selector set for each production-changing subbundle must come from `code_analytics_impacted_tests_get` against the real subbundle diff.

## Candidate workspaces

Supply only relevant runnable workspaces, but do not omit an affected suite:

- `tests/Solutions/CanDoItAll.Tests.Components.slnx`
- `tests/Solutions/CanDoItAll.Tests.Playwright.slnx` for named browser checkpoints
- `tests/Solutions/CanDoItAll.Tests.Unit.slnx` when non-Razor mapping/helpers are changed and owner tests live there
- `tests/Solutions/CanDoItAll.Tests.Integration.slnx` only when DI/composition behavior is changed
- `tests/Solutions/CanDoItAll.Tests.Stable.slnx` only when promoted or invalidated at SB09

## Invalid proof

The following does not close a subbundle:

- an unfiltered broad test run with no impacted-test evidence;
- a test command that discovers zero tests;
- only building the Web host;
- only testing the new neutral component without testing its Agent adapter;
- screenshots without inspecting normal and open-overlay states;
- manually seeded component state when the changed behavior belongs to a real producer/lifecycle;
- claiming behavior preservation because public signatures did not change.
