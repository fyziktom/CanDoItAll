# User verification handoff

Status: Implementation ready; final broad-test certification is conditional on authorization for a second Stable run.

## What changed

- Simple Chats now lives in MAF libraries and is composed as the tab immediately after Agents.
- `/chats` remains a compatibility route and preserves recognized Simple Chat/deep-link state.
- Dashboard usage, tokens, cost, provider/model rankings, and detail dialogs switch among Both, Agents, and Simple Chats.
- Simple Chat definitions use Identity, Runtime, and Output and revision tabs.
- Agent and Simple Chat settings use the same avatar picker with presets, upload/reset, and configured-provider AI generation.
- Simple Chat invocation attempts persist immutable pricing evidence; legacy records are visibly unpriced.

## Verified in the real UI

- Main and floating Agent messages completed with deterministic replies.
- Main and floating Simple Chat messages completed with deterministic replies.
- Both floating sources survived hide/reopen with transcript continuity.
- Scope totals added exactly; provider/model dialogs followed scope; SVG charts rendered.
- Definition inner tabs, shared avatar picker, upload, and OpenAI image generation were exercised.
- `/chats` redirect, reload, and recognized route state were verified.
- Viewport 1600x1000; zero console/page errors.

## Test evidence

- Focused Unit: 20/20, plus post-Stable exact affected classes 32/32.
- Focused Components: 36/36 and 6/6, plus post-Stable exact affected classes 9/9.
- Focused Integration: 22/22.
- Named Playwright: 5/5.
- One Stable run: Integration 856 pass/1 expected skip; Unit 6,262 pass/2 stale assertions; Components 1,033 pass/6 stale DI fixtures. All eight failing test contracts were then fixed and their exact classes pass. Stable was not rerun by bundle policy.

## Remaining risk/action

No known product defect remains. A second unfiltered Stable run requires explicit authorization because the bundle permits only one broad run; until then the FINAL certification label remains conditional.

Screenshot paths and SHA256 hashes are recorded in `proof/SB11/playwright-mcp-evidence.md`.
