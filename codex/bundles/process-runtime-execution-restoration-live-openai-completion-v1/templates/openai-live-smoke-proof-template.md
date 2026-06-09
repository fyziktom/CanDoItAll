# OpenAI Live Smoke Proof Template

## Configuration
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`: `<true|false>`
- Key source: `<environment/config name only, never value>`
- Provider/model: `<provider/model>`
- Budget limit: `<max tokens/cost estimate>`
- Timeout: `<duration>`

## Result
- Status: `<passed|skipped|failed>`
- Skip reason if skipped: `<reason>`
- Request hash: `<hash>`
- Response/output hash: `<hash>`
- Duration: `<duration>`
- Token metadata: `<if available>`
- Process run id: `<guid>`
- Step run id: `<guid>`
- Artifact ids: `<ids>`

## Safety
- No raw API key logged.
- No secret-bearing prompt or output logged.
- Failure categorized as provider/config/runtime/prompt/finalizer/artifact.
