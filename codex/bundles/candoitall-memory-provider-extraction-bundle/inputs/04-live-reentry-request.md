# Live Re-entry Request Preservation

## Request date

- 2026-07-05

## Raw request

The user asked a senior C# architect to analyze the full existing bundle at `C:\repositories\CanDoItAll\codex\bundles\candoitall-memory-provider-extraction-bundle`, not skip or remove any part of it, and improve the bundle so it matches the current `CanDoItAll` repository after later MAF refactoring.

The user also stated that the native Cognitive Memory repository is cloned at `C:\repositories\CanDoItAll.CognitiveMemory`.

The requested outcome is bundle preparation only. No application implementation should be performed yet.

## Normalized re-entry objective

- Re-open the prepared bundle as a stale initiative bundle.
- Preserve the existing phase/subbundle structure unless a correctness gap requires additive clarification.
- Ground corrections in the live repositories rather than the prior uploaded ZIP state.
- Update the bundle so generic memory providers work when no provider has been configured yet.
- Align the MAF integration phases with the current MAF contracts, provider/runtime tool model, workflow executor model, and existing memory source snapshot contracts.

## Hard constraints retained

- Do not implement production code.
- Do not remove subbundles or scope.
- Do not weaken the native Cognitive Memory extraction goal.
- Do not silently fall back to native Cognitive Memory, OpenAI, Qdrant, or any default provider when no memory provider is configured.
