# SB08: 08-opentelemetry-and-trace-correlation-proof

## Goal

Prove telemetry correlation or explicitly mark it guarded.

## Required work

- Check whether OpenTelemetryChatClient exists in the local assemblies.
- If unavailable, prove CanDoItAll telemetry boundary still correlates agent run, process run, tool call, and journal entry.
- Add diagnostic output or test that prevents double-wrapping.
- Update docs with telemetry decision.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB08` are filled and the downstream dependency is safe.
