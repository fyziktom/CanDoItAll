# SB04: 04-finalizer-message-injection-or-context-provider-hardening

## Goal

Make finalizer/structured-output instructions robust in the MAF 1.6 tool loop.

## Required work

- Adopt IChatMessageInjector if available and practical.
- If unavailable, harden MessageAIContextProvider/finalizer fallback and document why.
- Test that finalizer instructions survive multi-tool loops, approval pending/resume, malformed response repair, and streaming/non-streaming execution.
- Ensure no duplicate finalizer instruction causes confused output.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB04` are updated and downstream subbundles can rely on it.
