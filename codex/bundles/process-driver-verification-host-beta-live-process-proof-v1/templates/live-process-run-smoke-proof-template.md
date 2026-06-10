# Live Process Run OpenAI Smoke Proof Template

## Environment
- `CANDOITALL_RUN_LIVE_AGENT_VALIDATION`: `<present/absent>`
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`: `<present/absent>`
- `OPENAI_API_KEY`: `<present/absent>`; never print value
- `CANDOITALL_LIVE_OPENAI_MODEL`: `<model>`
- `CANDOITALL_LIVE_OPENAI_MAX_TOKENS`: `<integer>`
- `CANDOITALL_LIVE_OPENAI_TIMEOUT_SECONDS`: `<integer>`

## Process proof
- Process definition/template key:
- Run id:
- Step id:
- Execution route: direct-agent / workflow-backed
- Provider/model:
- Final run status:
- Artifact ids:
- Managed file/readback summary:
- Redacted diagnostics:
- Token/cost estimate if available:

## Denial/skip policy
- If any required env var is absent, record `SKIPPED_POLICY` and do not claim live-provider functionality.
- If provider call fails, record failure category and preserve deterministic fallback results separately.
