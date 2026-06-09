# SB020 OpenAI Live Smoke Proof

## Configuration
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`: `false`
- Key source: `OPENAI_API_KEY` present; value redacted
- Provider/model: not exercised
- Budget limit: absent
- Timeout: absent

## Result
- Status: skipped
- Skip reason if skipped: `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE` is not `true`; explicit budget and timeout are absent.
- Request hash: N/A
- Response/output hash: N/A
- Duration: N/A
- Token metadata: N/A
- Process run id: N/A
- Step run id: N/A
- Artifact ids: N/A

## Safety
- No raw API key logged.
- No secret-bearing prompt or output logged.
- Failure category: config/opt-in skip, not provider runtime failure.

## Proof
- Skip transcript: `bundle://proof/SB020/transcripts/live-openai-smoke-skipped.txt`
- Source/policy assertions: `bundle://proof/SB020/transcripts/live-openai-smoke-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB020/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB020/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
