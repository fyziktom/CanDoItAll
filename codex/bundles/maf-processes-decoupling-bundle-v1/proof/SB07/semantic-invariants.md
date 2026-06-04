# SB07 Semantic Invariants

## Invariant `SB07-INV-001`

- Invariant ID: SB07-INV-001

- Source raw note: MAF and Processes must be decoupled without simplifying or omitting behavior.
- Expected behavior: Real app composition registers exactly one Processes-owned `IAgentRuntimeToolProvider`; that provider exposes all 23 process tools; MAF attaches no process tools when no providers are registered; process outbox, tool-receipt, and current-run artifact-lineage behavior still pass.
- Disallowed shallow implementation: Passing build by deleting process tools, weakening tests, relying on count-only parity, testing only direct service registration without `TestApplication` composition, or ignoring process evidence/receipt behavior.
- Failing-first test and transcript: `bundle://proof/SB07/transcripts/runtime-tool-provider-composition-tests.txt` fails if real composition omits the provider or any expected tool; `maf-zero-provider-tests.txt` fails if MAF leaks process tools without providers; `process-receipt-semantics-tests.txt` fails when required tool receipts are missing.
- Passing test and transcript: `bundle://proof/SB07/transcripts/runtime-tool-provider-composition-tests.txt`, `bundle://proof/SB07/transcripts/maf-zero-provider-tests.txt`, `bundle://proof/SB07/transcripts/process-outbox-tests.txt`, `bundle://proof/SB07/transcripts/process-receipt-semantics-tests.txt`, `bundle://proof/SB07/transcripts/process-artifact-lineage-tests.txt`, and `bundle://proof/SB07/transcripts/solution-build.txt` pass.
- Changed source files and hashes: `bundle://proof/SB07/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB07/source-assertions/runtime-composition-source-assertion.txt`.
- Red-team negative case: Missing provider registration, missing exact process tool, zero-provider tool leakage, missing required receipt, or wrong/stale lineage would be caught by the targeted SB07 tests.
- Browser validation: N/A. SB07 exercised service/runtime paths only and did not change or render a UI route.
- Downstream dependency check: SB08 may start because SB07 proves real composition and runtime smoke, not only compile-time structure.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB07 adds runtime/integration proof only; it introduces no persisted production signal, state, record, or event. | N/A | N/A | N/A |
