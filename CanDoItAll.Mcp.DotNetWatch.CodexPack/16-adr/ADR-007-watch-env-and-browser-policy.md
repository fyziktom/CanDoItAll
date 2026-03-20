# ADR-007 — Apply watch-friendly environment defaults and suppress auto browser launch

## Status
Accepted

## Context
The server is intended to cooperate with an external browser automation tool or a human-controlled browser loop. Automatic browser launch and noisy watch output add nondeterminism.

## Decision
Apply watch-friendly defaults:
- `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1`
- `DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1`
- `DOTNET_WATCH_SUPPRESS_EMOJIS=1`

Recommended optional default:
- `DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH=1`

## Rationale
- no interactive prompt on rude edits
- browser lifecycle stays under client control
- cleaner logs for automation
- fewer accidental UI side effects

## Consequences
Positive:
- better compatibility with Codex + Playwright-style workflows

Negative:
- when browser refresh is suppressed, the client must explicitly refresh

## Follow-up
Make browser refresh suppression configurable if repo-specific behavior requires it.
