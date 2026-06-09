# SB019 Live OpenAI Configuration Policy

## Status
Completed.

## Decision
Live OpenAI smoke is disabled for this run.

## Configuration Check
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE`: absent
- OpenAI key source: `OPENAI_API_KEY` present; value not printed
- Explicit budget: absent
- Explicit timeout: absent

## Policy Result
The bundle policy requires all of the following before running a live smoke:
- `CANDOITALL_ENABLE_LIVE_OPENAI_SMOKE=true`
- API key present
- Explicit budget configured
- Explicit timeout configured

Because the opt-in flag and explicit budget/timeout are absent, live smoke must be skipped.

## Proof
- Configuration transcript: `bundle://proof/SB019/transcripts/live-openai-configuration-check.txt`
- Source/policy assertions: `bundle://proof/SB019/transcripts/live-openai-policy-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB019/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB019/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
