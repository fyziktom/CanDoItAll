# Final Agent Chat UI parity

## Environment

- Host: `src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`
- URL: `http://127.0.0.1:5218`
- Viewport: 1600 × 1000
- Browser driver: Playwright MCP
- Database selector: existing local database, continued through the UI
- Cleanup: browser closed and isolated host PID 30956 stopped

## Main Agent chat

- Opened `/agents?tab=chat` with `.NET Application Developer` selected.
- Sent `Reply with exactly: MAIN AGENT CHAT OK` through the visible composer.
- Received the exact assistant response `MAIN AGENT CHAT OK`.
- Verified persisted four-message thread, `Completed` execution state, runtime details, auto-approval state, markdown/copy/timestamps/token metadata, attachment controls, unavailable voice state, and a working Prompt Gallery dialog with 111 matching items.

## Floating Agent chat

- Opened the global Agent chats catalog, selected Delivery QA Observer, and started a new floating chat.
- Sent `Reply with exactly: FINAL FLOATING AGENT CHAT OK` through the floating composer.
- Received the exact assistant response `FINAL FLOATING AGENT CHAT OK`; the run persisted as `Completed` with two messages and 15 execution steps.
- Detached from application context and followed the current surface again.
- Hid the window with `Keep active`, verified the Active chats entry, reopened it, and confirmed transcript and affinity persistence.
- Opened history and verified the new two-message thread alongside the prior regression thread.
- Used the close dialog's `Stop chat` path and verified `0 active` / `No chats are being kept active`.
- Visual composition, layering, focus, and internal scrolling remained usable at the required desktop viewport.

## Settings and consumers

- Opened the Agent details editor and verified Identity fields.
- Verified Runtime provider `OpenAI default`, model `gpt-5.6-luna`, thinking effort `Medium`, FrameworkManaged history, and approval policy.
- Saved the unchanged strongly typed settings through the UI and observed the selected Agent's updated timestamp at 16.08.2026 11:26.
- Opened `/processes`, selected `Manager chat`, and verified the Delivery Manager selector, Process context state, shared thread/workspace surface, composer, attachment action, and Agent context action.

## Diagnostics

- Browser console: 0 errors, 0 warnings.
- Blazor initializer and negotiate requests: 200 OK.
- Voice recording and destructive/error scenarios remain environment-dependent/manual; deterministic callback/state coverage is included in the 990/990 Components result.

## Evidence

- `proof/SB09/browser/final-floating-agent-chat-completed.png`
- `proof/SB09/browser/final-agent-settings-runtime.png`
- `proof/SB09/browser/final-process-manager-chat.png`
- `proof/SB09/browser/final-console-warnings.txt`
- `proof/SB09/browser/final-network-requests.txt`
