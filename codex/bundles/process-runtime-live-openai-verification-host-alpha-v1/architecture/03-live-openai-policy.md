# Live OpenAI Policy

## Required behavior
If an OpenAI API key is present and the user has not explicitly disabled live tests, run a minimal live smoke.

## Required environment for command
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`
- `CANDOITALL_LIVE_OPENAI_MAX_TOKENS=800` or lower
- `CANDOITALL_LIVE_OPENAI_TIMEOUT_SECONDS=120` or lower

Codex may set these variables for the command invocation when `OPENAI_API_KEY` is already present. It must never print the key.

## Acceptable outcomes
- **Pass:** live provider returns a small response, process/direct-agent smoke records run/step/provider metadata, and no secrets are logged.
- **Provider failure:** capture redacted provider error, classify as live-provider failure, keep deterministic tests green, and open a remediation item. Do not call it a live pass.
- **Skip:** allowed only when API key is absent or explicit opt-out is present. Missing opt-in alone is not enough in this bundle because the user explicitly requested live testing and credits are available.
