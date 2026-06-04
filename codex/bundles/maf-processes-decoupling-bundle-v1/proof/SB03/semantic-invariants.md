# SB03 Semantic Invariants

## Invariant `SB03-INV-001`

- Invariant ID: SB03-INV-001

- Source raw note: MAF and Processes must be decoupled in small safe steps without simplifying process runtime behavior or approval behavior.
- Expected behavior: MAF resolves `IAgentRuntimeToolProvider` instances from DI, invokes them deterministically, attaches their tools, rejects duplicate provider tool names, and preserves the old internal process tool path until migration.
- Disallowed shallow implementation: Add Tooling contracts but never call providers; silently dedupe duplicate provider tools; or omit approval wrapping for provider-supplied process mutation tools.
- Failing-first test and transcript: `MafAgentRuntimeToolProviderComposition_rejects_duplicate_provider_tool_names` in `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt` fails a silent-shadow implementation.
- Passing test and transcript: `bundle://proof/SB03/transcripts/maf-tool-provider-composition-tests.txt` passes 5 tests for zero providers, fake providers, duplicate rejection, approval wrapping, and provider failure diagnostics.
- Changed source files and hashes: `bundle://proof/SB03/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB03/source-assertions/provider-composition-source-audit.txt` and `bundle://proof/SB03/source-assertions/old-process-path-still-present.txt`.
- Red-team negative case: A provider returning the same tool name as an already registered runtime tool throws instead of shadowing, so SB04 cannot accidentally hide missing or duplicate process tools.
- Downstream dependency check: SB04 can start because provider composition is active while the current MAF process builder remains in place for compatibility.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB03 introduces composition behavior, not a persisted production signal, state, record, or event. | N/A | N/A | N/A |
