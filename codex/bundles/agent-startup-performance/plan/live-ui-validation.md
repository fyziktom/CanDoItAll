# Real Playwright MCP UI Validation

Required during execution on both native5032 and Docker5214. No browser activity or agent calls occur during preparation. UI actions must use real rendered controls, not browser-evaluated fetch/API calls. API/read-only persisted evidence may corroborate IDs and content.

## Target And Fixtures

Use separate persistent browser contexts for each localhost origin to avoid cookie/antiforgery interference; one page per host, 1920×1080. If the available MCP cannot provide isolated contexts, use isolated browser sessions sequentially and prove origin/cookie separation before sends.

- Native5032 candidate: `/projects/f28c07cd-982c-4d2d-bcf2-3e60a32eca72/structure`, existing QuotationPDFs Tests project, an existing agent authorized to read its PDF/spreadsheet (Spreadsheet Analyst is a discovery hint).
- Docker5214 candidate: `/projects/e008de34-69eb-4fea-9b47-b4c23991b17d/structure`, existing Calculator, Portfolio Architect; shared-provider model should display its readable name.
- Resolve current identity, capabilities and authorized assets from UI/read-only catalogs. If a candidate is unavailable, select an existing equivalent approved fixture and document the match before baseline; do not silently create projects/providers/credentials or alter policy.

Use existing test IDs after inspecting current DOM: `shell-agent-chats-action`, `floating-agent-catalog-window`, `conversation-shell-filter-agents`, `chat-prompt-input`, `chat-send-button`, `chat-execution-entry`, `chat-execution-summary`, `agent-execution-log-dialog-body`, `agent-execution-log-dialog-entry`. Approval IDs are discovered; selectors use `chat-approval-approve-{approvalId}` / `chat-approval-reject-{approvalId}`. Never rely on stale element refs.

## Mandatory Matrix

| ID | Actions on BOTH hosts | Observable acceptance |
|---|---|---|
| UI01 | Fresh conversation through floating list; send fixed genuine question; observe preparation and answer. | New session/run; correct agent/readable model; all applicable stages; actual provider usage; meaningful answer and persisted Completed state. |
| UI02 | Native: read actual authorized PDF and spreadsheet, compare concrete prices. Client: read actual Markdown proposal and SVG, compare arithmetic scope/layout/history. | Actual tool names/call IDs/receipts and results; verify at least two concrete answer facts against independently read source assets. An LLM claim to use tools or nonempty text is insufficient. |
| UI03 | In same conversation ask about a specific prior answer fact and request one new relevant file-backed check. | Same session, distinct run, prior context retained, fresh tool invocation, no duplicated first turn. |
| UI04 | While active hide/close using existing Keep active behavior; reopen from Active. Reload after completion; open execution history/tool details. | Work continues; identical session/messages; ordered durable progress logs and tool receipts survive reload; no missing/duplicated turn or stuck spinner. |
| UI05 | Explicitly request stat/read of a confirmed nonexistent path inside that project's already authorized area. | Tool really executes; error reaches agent/UI without fabricated file content, writes or secret leakage; state settles. A recovered tool error may correctly end Completed. |
| UI06 | Inspect running close dialog/Stop restriction; Cancel or Keep active. | Running Stop is disabled/unavailable or rejects closure according to existing contract. No accidental cancellation; no invented Cancel-run feature. |

## Approval Matrix

- 5032: if the existing demonstrated conversion/tool policy requires approval, trigger that same authorized benign flow; inspect tool/arguments, approve once; in a separate run reject once. No execution before approval; accepted action resumes once; rejected action never executes; approval/checkpoint IDs and decisions persist after reopen.
- 5214: record the existing policy. If Auto Approve is configured, prove expected tool execution without an unexpected prompt; **do not alter policy to manufacture HITL**. If approval is already applicable, perform matching accept/reject proof.
- Pending approval handle closure is not rejection: when applicable, close/reopen the quick-access handle and verify the durable approval remains pending.
- “Not applicable” requires evidence of the current policy/capability, not convenience. An expected approval control missing or broken is a failure.

## Explicit Boundary: Cancellation And Terminal Failure

Floating-list Stop is **not** request cancellation. See `FloatingAgentChatArchitectureTests.Stop_rejects_a_running_chat` and `FloatingAgentChatCloseDialog.razor`. True cancellation, deterministic provider failure, and crash/persistence interruption are mandatory isolated automated tests in the test-selection plan. Do not kill live apps, disconnect providers, change tokens/URLs, or alter approval policy to induce them.

## UI Composition Contract And Screenshot Review

- Primary surface: existing transcript/composer and progress; supporting content stays in existing tool/progress/history overlays.
- Stats: existing compact status/usage badges only; no new cards or page layout.
- List/editor: preserve Available/Active floating catalog and existing chat windows; no new permanent editor.
- Textarea/dialog sizing: preserve existing composer and log dialog; inspect long actual content at the chosen desktop viewport.
- First viewport: identity, readable model, composer and relevant actions visible. Transcript/log body owns scrolling; header/footer actions remain accessible. No lateral overflow or layered overlay clipping.
- Capture and **inspect** normal completed chat, active progress/tool details, history/reopen and relevant error/approval/close dialog. Record readability, layering, clipping, scroll ownership, keyboard/action reachability and footer visibility. Existing file-browser CSS and model-name fixes must remain intact.
- No mobile tuning or BaseLib changes; if UI markup unexpectedly needs editing, reopen scope and use Components MCP before design.

## Evidence Per Row

Record origin/build/context, fixture IDs, session/run/operation/tool/approval IDs, exact MCP actions/assertions, tool result verification, safe console/network failures, screenshot paths and explicit visual findings, persisted-state correlation and Pass/Fail/conditional N/A. Store under `proof/SB03/ui/5032/` and `proof/SB03/ui/5214/`. Missing real UI proof on either host blocks closure.
