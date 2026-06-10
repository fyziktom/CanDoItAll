# Live OpenAI Process Proof Policy

## Current classification
The previous live proof runs `LiveSpecialistAgentScenarioIntegrationTests`, which verifies AgentFramework specialist agents over OpenAI. It is useful, but it is not a Process runtime live proof.

## Required new proof
Add an opt-in live process-run smoke:

- It must create/start a small process run through the normal process service/API path.
- It must route at least one direct-agent or workflow-backed role through live OpenAI.
- It must finalize the step/run or record a clear typed blocked/recovery state.
- It must project at least one managed artifact/evidence record or explicitly prove why not.
- It must read back run detail and artifact/diagnostic state.

## Required env gates
The test may run only when all are true:

- `CANDOITALL_RUN_LIVE_AGENT_VALIDATION=true`
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`
- `OPENAI_API_KEY` or approved equivalent is present
- `CANDOITALL_LIVE_OPENAI_MAX_TOKENS` is present and within allowed cap
- `CANDOITALL_LIVE_OPENAI_TIMEOUT_SECONDS` is present and within allowed cap
- `CANDOITALL_LIVE_OPENAI_MODEL` is present or resolves to a safe configured default

## Secret policy
- Do not print secret values.
- Do not persist raw OpenAI responses when they may contain secrets.
- Store only redacted diagnostics, labels, token/cost estimate where available, model name, timeout, and pass/fail reason.
