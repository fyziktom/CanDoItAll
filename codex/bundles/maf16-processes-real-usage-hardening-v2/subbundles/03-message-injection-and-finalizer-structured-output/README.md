# SB03: 03-message-injection-and-finalizer-structured-output

## Goal

Use MAF 1.6 message injection where it improves finalizer/guardrail reliability.

## Required work

- Evaluate replacing prompt-concatenated finalizer instructions with IChatMessageInjector or equivalent 1.6 mechanism.
- Keep backward-compatible adapter if injection is not supported for all provider transports.
- Add tests proving finalizer instructions survive tool loop and streaming/non-streaming execution.
- Ensure structured output finalizer cannot be skipped or duplicated.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB03` are updated and downstream subbundles can rely on the behavior.
