# Hardening prompt

Perform a hardening pass on the server.

## Focus areas
- stdout discipline
- log redaction
- path guard enforcement
- env whitelist enforcement
- stale process cleanup safety
- diagnostics quality
- watch environment defaults
- watch exclusions guidance
- cross-platform process stop behavior
- resilience to unexpected exit and timeout scenarios

## Expected actions
- fix weak or ambiguous error messages
- make diagnostics more actionable
- ensure all major flows emit structured events/correlation IDs
- add or tighten tests
- update docs/comments where implementation diverged from the original plan

## Deliver
- hardening changes
- security-impact summary
- updated risk list
