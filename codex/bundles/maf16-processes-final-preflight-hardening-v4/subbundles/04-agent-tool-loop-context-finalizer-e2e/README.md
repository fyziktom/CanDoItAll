# SB04: 04-agent-tool-loop-context-finalizer-e2e

## Goal

Prove context providers and finalizer survive real tool-loop execution.

## Required work

- Create deterministic agent/tool-loop test using MAF runtime path.
- Ensure `MessageAIContextProvider` content reaches the agent during tool invocation.
- Ensure finalizer/structured output contract is preserved after tool calls and approval continuation.
- Test streaming and non-streaming if both paths are supported.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB04` are filled and the downstream dependency is safe.
