# CP2 browser parity — SB05

Host: isolated fresh Development build on `http://127.0.0.1:5045` using per-project artifacts output.

## Empty state

- Opened the floating Agent catalog and started `.NET Application Developer`.
- Verified the Agent header, context strip, `New exploration thread`, zero-message badges, empty transcript copy, composer, prompt-gallery, attachment, send, voice, and window controls.
- The initial Dashboard send exercised the preserved explicit error path: `AgentChatContextPositionUnavailableException` because no module context was available. No fallback hid the error.

## Contextual send and populated state

- Opened the floating chat from `Agents / Overview`; the context strip showed `Context access allowed` and followed the current surface.
- Sent a markdown-sample request through the floating UI.
- During execution, verified the pending user bubble, disabled send/new-thread/attachment controls, status text, live execution stream, and auto-approve policy entry.
- The operation completed in 14 seconds with two persisted messages and 15 execution steps.
- Verified heading, paragraph, list, fenced code, copy actions, timestamp/token metadata, transcript scrolling, completed status, and usable composer.
- Opened `Runtime details`; verified selected execution/provider/model/transport, completed timeline, and metrics.
- Current-navigation browser console result: zero errors.

Screenshots:

- `proof/SB05/browser/SB05-cp2-populated-floating-chat.png`
- `proof/SB05/browser/SB05-cp2-runtime-details.png`

Decision: desktop CP2 parity passes. The DOM is intentionally decomposed into neutral components; visible behavior and Agent-owned runtime operations remain compatible.
