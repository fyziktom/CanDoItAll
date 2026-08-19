# CP3 floating browser parity

Host: isolated Development Web instance at `http://127.0.0.1:5217`, 1600 by 1000 desktop viewport. The proof host and Playwright browser were stopped after inspection.

## Catalog and active lifecycle

- Verified the catalog starts closed and opens from the existing Agent chats shell action.
- Verified exact Agent labels, search, context/access badges, 28-agent catalog, internal clipped participant scroll, active tab, and zero/one active states.
- Browser geometry was 560 by 720 at x=1024/y=16; the body overflow remained hidden and the overlay remained above the workspace.
- Started `Delivery QA Observer`, hid it through Keep active, observed `Kept active / Ready`, reopened it, and finally stopped the handle through the existing confirmation dialog.

## Actual Agent chat

- Sent `Reply with exactly: FLOATING CHAT REGRESSION OK` through the floating composer.
- The Agent completed in 14 seconds and returned `FLOATING CHAT REGRESSION OK`.
- Reopening the hidden handle preserved the same durable thread and both messages.
- Opened Agent thread history, observed the persisted two-message thread, and reopened it from the history overlay.

## Context and settings

- Verified `Agents · Overview`, context access allowed, Follow current surface, Detach, detached state, and Follow context restoration.
- Changed the page surface to Floating chat and verified the catalog context updated to `Agents · Floating chat`.
- Inspected lifecycle values and constraints without saving: retention 10 (1..1440), maximum active 12 (1..50).
- Verified Agent-only prepared stock remained adjacent and unchanged: maximum prepared 0, adaptive enabled, prepared retention 10.

Playwright console inspection returned zero warnings and zero errors for the proof navigation.

Decision: CP3 floating/settings parity passes.
