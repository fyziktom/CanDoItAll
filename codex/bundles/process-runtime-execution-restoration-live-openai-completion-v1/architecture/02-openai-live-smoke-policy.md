# OpenAI Live Smoke Policy

## Opt-in only
The live OpenAI smoke must run only when all are true:
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`
- OpenAI API key is present in the expected existing configuration path or environment variable.
- Test budget and timeout are explicitly configured.

## Required behavior
- Never log API keys or complete secret-bearing environment values.
- Use a tiny request and small max output.
- Capture provider/model, request id or hash, duration, token/count metadata when available, status and output hash.
- Do not assert on exact natural-language text.
- If disabled or missing credentials, skip with an explicit reason and do not count as a pass for live-provider functionality.
- Live test failure should not be hidden by deterministic tests; it must be classified as provider/config/runtime/prompt/finalizer/artifact failure.

## Scope
The live smoke should validate provider plumbing through current MAF/direct-agent execution in a minimal process step. It should not scaffold real projects, write workspace files through drivers, or perform uncontrolled command execution.
