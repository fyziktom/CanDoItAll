# Blocked Send with an editable draft: browser source

Status: source prepared only in the managed continuation checkout. No build, browser, server, process or database was started. Parent execution follows the matched store-cost comparison; this slice is separate from historical receipt review and its ten-file cold Process extraction.

Exactly one new `[Fact]`, no theories/skips, with class categories `Playwright` and `HostPlatform`:

```text
FullyQualifiedName=CanDoItAll.Tests.Playwright.AgentChatSendAffordanceBrowserTests.Unconfirmed_outcome_blocks_Send_without_disabling_draft_editing
```

The fixture appends `Unconfirmed`, preserving all prior scenario numbers. It sets `IsSendBlocked=true`, `IsBusy=false`, no approvals and a nonempty editable draft. A sandbox-only counter increments for every current-revision Send callback; it deliberately does not suppress a callback based on the presentation flag, so a renderer regression cannot pass because the fixture silently drops the action. The counter persists across the explicit switch to Transcript.

The existing specimen adds read-only count output and busy/blocked/approval-count attributes. It keeps the existing `AgentChatSurface`, composer, draft callback and layout. The independent launcher adds only the AgentChat enum case, existing sandbox/Parity binary mapping and distinct evidence folder. Its lifecycle implementation is unchanged. There are no product services, new host abstractions, dependency/project changes, style rules or JavaScript additions.

The browser arrangement uses Chromium at 1920×1080. It verifies the blocked/nonbusy/no-approval state, edits the exact draft through the real input callback, observes DraftChanged and zero Send callbacks, then pointer-clicks the observed disabled button bounds without force or DOM mutation. Draft, intent and counter must remain unchanged. It then changes the actual scenario selector to Transcript, verifies enabled idle Send and zero prior callbacks, and sends normally. Exactly one callback, Send intent and cleared draft are required. Both screenshots require no horizontal overflow. Console/page errors and same-origin failed responses/requests are retained; errors and host failure/critical logs must be absent.

Expected artifacts when the parent executes:

- `output/playwright/agent-chat-send-affordance/<UTC-and-Guid>/blocked-editable-draft.png`
- `output/playwright/agent-chat-send-affordance/<UTC-and-Guid>/idle-after-send.png`
- `browser-evidence.json` and `server.log` in that directory; `failure.png` when capture is possible after a failure.
- Existing launcher hashes/logs under `artifacts/agent-chat-send-affordance/sandbox-hosts/<Guid>/`.

The parent must independently inspect the blocked screenshot and verify owned host PID/start identity, listener and test-environment cleanup after the browser/context are disposed. The inherited host's process/pump waits remain subject to the parent's outer deadline and cleanup guard; this source does not claim to make those waits bounded. The sandbox has no real dispatch backend, so the callback proof is a browser affordance proof, not owner-write or receipt-recovery evidence. Existing native component guards remain separate evidence.

The Components skill and canonical UI seam were applied using the actual existing component source. No callable `components_recommend`, `component_get` or library-list capability was available in the tool inventory; no MCP result is claimed. No new component selection or composition was needed. Only the AgentChatSpecimen rendering-register row changed; its isolated hash patch is recorded separately so unrelated historical register changes are not transferred.

Source-only checks: three owned C# files parse with zero Roslyn syntax errors; the new class has exactly one Fact and the two stated traits. Historical cold Process input hashes remain unchanged. Compilation, one-row discovery, browser execution, visual inspection, final rendering enforcement and portability-static enforcement remain parent-owned and unexecuted for this source candidate.
