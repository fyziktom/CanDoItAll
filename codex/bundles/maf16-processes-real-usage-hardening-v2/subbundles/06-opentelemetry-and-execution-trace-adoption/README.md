# SB06: 06-opentelemetry-and-execution-trace-adoption

## Goal

Handle the 1.6 OpenTelemetry wrapper change deliberately.

## Required work

- Audit whether CanDoItAll uses OpenTelemetryAgent or OpenTelemetryChatClient wrappers.
- Prevent double wrapping and missing spans.
- Ensure tool receipts, execution logs, context contribution traces, finalizer invocations, and process journal events remain correlated.
- Add trace correlation tests or source assertions where runtime telemetry tests are impractical.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB06` are updated and downstream subbundles can rely on the behavior.
