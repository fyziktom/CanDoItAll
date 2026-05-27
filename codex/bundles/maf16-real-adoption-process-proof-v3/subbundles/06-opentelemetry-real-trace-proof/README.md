# SB06: 06-opentelemetry-real-trace-proof

## Goal

Provide real OpenTelemetry/trace proof after MAF 1.6.

## Required work

- Audit whether OpenTelemetryChatClient is auto-wired or whether CanDoItAll wraps telemetry explicitly.
- Add a trace correlation test or diagnostic endpoint proof linking agent run id, process run id, tool call, and journal entry.
- Ensure no double-wrapping creates duplicate spans.
- Ensure missing telemetry does not break process runtime.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB06` are updated and downstream subbundles can rely on it.
