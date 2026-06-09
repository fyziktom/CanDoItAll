# Live OpenAI E2E Policy

This bundle allows live OpenAI process smoke tests because credits are available, but only under explicit guardrails.

## Required configuration

- OpenAI key must be supplied through the existing secure configuration/environment mechanism.
- Never commit keys, screenshots containing keys, request bodies with secrets, or raw provider payloads containing tokens.
- The test must be opt-in using a clear environment flag such as `CANDOITALL_RUN_LIVE_OPENAI_PROCESS_TESTS=true`.
- The model must be configurable and default to the cheapest reliable available configured model in the app configuration.
- A hard timeout, max attempts, max output size, and estimated token budget must be recorded.
- The test must be skippable when the key/flag is absent and must not fail CI by default.

## Scope

Allowed:
- one minimal `.NET` create/modify run,
- one minimal business-analysis run,
- collecting sanitized run status, artifacts, diagnostics, and final state.

Denied:
- logging API keys or secrets,
- broad live test matrix,
- creating arbitrary external files outside configured test workspace,
- shell execution outside normal process tools unless the process runtime already explicitly supports and audits it,
- using live tests as the only proof. Deterministic tests must remain primary.
