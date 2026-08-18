# Browser Agent Parity

- Driver: Playwright MCP against the real managed Web app.
- Session: `app_2be61046b90443008ae262a510e4a208`.
- URL: `http://127.0.0.1:5217`.
- Viewport: 1600x1000.
- Revision: `simple-chats-ui-integration:1:g0`, confirmed.
- Watch state: Healthy / WaitingForChanges, generation 0.

## Observations

- Agent catalog rendered 28 technical agents, 6 providers, and 127 capabilities without console errors.
- HR Agent settings opened at 1600x1000; the Runtime tab and all other tabs were reachable, and Clear/Save were visible. No setting was mutated.
- Main HR Agent chat rendered its durable transcript, approval controls, and composer. `.chat-conversation-flow` was the single transcript scroll owner.
- Floating HR Agent chat opened at `(16,16)` with size `760x720`; its composer ended at y=702.5 and remained inside the viewport.
- Hide chat opened the explicit Cancel/Keep active/Stop chat decision. Keep active produced one Ready retained chat; Open restored the same thread identity.
- Project Structure contextual catalog rendered at `(1024,16)` with size `560x720`, identified `Project structure · Garden`, and listed 23 eligible agents. Searching HR Agent returned `No agents can read the current context`, proving fail-closed access filtering.
- `/chats` returned HTTP 404 before CP1, proving route activation had not leaked forward.

## Screenshots

- `bundle://proof/SB05/screenshots/SB05-agents-catalog-1600x1000.png`
- `bundle://proof/SB05/screenshots/SB05-agent-settings-runtime-open-1600x1000.png`
- `bundle://proof/SB05/screenshots/SB05-agent-main-chat-1600x1000.png`
- `bundle://proof/SB05/screenshots/SB05-agent-floating-chat-open-1600x1000.png`
- `bundle://proof/SB05/screenshots/SB05-project-structure-contextual-agent-1600x1000.png`
- `bundle://proof/SB05/screenshots/SB05-simple-chat-route-absent-404-1600x1000.png`
