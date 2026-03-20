# Playwright extension prompt

Prepare the MCP server and docs for cooperation with Playwright or another browser MCP tool.

## Goal
Do not embed browser automation into this server yet. Instead, make the runtime orchestration browser-friendly.

## Improve or verify
- `app_start` returns observed URLs
- `app_wait` supports deterministic readiness checks before browser steps
- watch defaults suppress automatic browser launch
- docs explain the recommended UI loop:
  1. start or reuse session
  2. edit files
  3. wait for quiet period
  4. wait for healthy
  5. refresh browser
  6. validate UI
- add example status/log payloads that a browser agent can consume

## Deliver
- docs changes
- any small contract tweaks that improve browser-tool interoperability
