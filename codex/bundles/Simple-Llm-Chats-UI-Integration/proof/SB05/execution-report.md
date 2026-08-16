# SB05 Execution Report

## Outcome

CP1 passed. The accumulated SB02-SB04 hardening is green in Component and Unit workspaces, focused Integration coverage is green, the one additional full-suite CAS failure passed on exact isolated retry, and real-browser Agent parity passed at 1600x1000. Simple Chat UI activation is allowed for SB06; floating Simple Chat integration remains locked.

## Test selection and execution

- Final-diff CodeAnalytics correlation: `code-analytics_9baf462acc914870b572970e410b0448`.
- Unit, Integration, Component, and Playwright workspaces were healthy. Static containment was incomplete with low confidence and selected `AllSuppliedSuites` because the 5,000-member budget, declaration/contract shapes, dynamic/reflection use, and unresolved changed symbols triggered `TIA2001`, `TIA3002`, `TIA3004`, and `TIA3001`.
- The subbundle's explicit full-Playwright prohibition overrides that fallback; all Component/Unit/Integration tests were run, plus only the named browser scenarios.
- Component: 1,007 passed, 0 failed, 0 skipped.
- Unit: 6,229 passed, 0 failed, 0 skipped.
- Integration: 850 passed, 4 failed, 1 skipped out of 855. Three failures are the previously documented unchanged baseline defects. The fourth cross-process CAS test passed 1/1 on immediate exact isolated retry, identifying suite-order/database contamination rather than a product regression.
- One live local Ollama test remained the expected environment-dependent skip.

An initial parallel Component/Unit attempt was discarded because restricted AppData access and shared database teardown contention made it an invalid environment. The recorded proof comes from sequential unrestricted runs.

## Browser parity

The managed Web app (`app_2be61046b90443008ae262a510e4a208`) was healthy at `http://127.0.0.1:5217`, revision `simple-chats-ui-integration:1:g0`, with a 1600x1000 Playwright MCP viewport.

- Agent catalog: 28 technical agents, 6 providers, and 127 capabilities rendered with no console error.
- Settings: the HR Agent Runtime tab opened full-viewport with all configuration tabs and Clear/Save controls visible.
- Main chat: an existing durable HR Agent transcript, approval state, and composer rendered; only the transcript flow owned scrolling.
- Floating: a new durable HR Agent chat opened, Keep active retained it, Active chats reported one retained chat, and Open restored the same thread.
- Context: Project Structure exposed `Project structure · Garden`, Ready state, and 23 access-eligible agents. Searching for the inaccessible HR Agent produced the expected fail-closed empty state.
- Activation fence: `/chats` returned expected HTTP 404 before CP1. The resulting navigation console error is the expected browser report for a missing route, not an application regression.

Screenshots are stored under `bundle://proof/SB05/screenshots/`.

## Architecture and progression

Fresh scoped snapshot `snap-20260816214112-d26d371e` has no blocking errors and the same structural hash as the earlier baseline. No project reference or new cycle exists. CP1 is Pass: set `simpleChatUiActivationAllowed=true`, unlock SB06, and leave `floatingIntegrationAllowed=false` until CP2.

## Reopen triggers

Reopen SB05 on any later regression to Agent settings/main/floating/contextual behavior, any change to an SB02-SB04 owned public contract or authorization/lifecycle rule, a new dependency cycle, missing proof artifact, unexpected zero discovery, or failure of a required selector.
