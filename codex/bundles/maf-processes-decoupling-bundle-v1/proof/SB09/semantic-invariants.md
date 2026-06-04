# SB09 Semantic Invariants

## Invariant `SB09-INV-001`

- Invariant ID: SB09-INV-001

- Source raw note: MAF and Processes must be decoupled without simplifying or omitting behavior.
- Expected behavior: Final closure proves direct MAF -> Processes process-tool coupling is removed, guarded, documented, and runtime-smoked while preserving all 23 process tools, process access checks, approval policy, process outbox behavior, tool-receipt semantics, and artifact-lineage behavior.
- Disallowed shallow implementation: Passing build by deleting process tools, weakening tests, relying on counts instead of exact tool names, hiding a MAF Processes dependency in docs/source, or claiming process-core/driver extraction is done.
- Failing-first test and transcript: `bundle://proof/SB09/transcripts/maf-hidden-dependency-scan.txt` fails on hidden MAF dependency markers; `maf-static-dependency-guard-test.txt` fails on direct MAF Processes/project/source/doc markers; `agent-tool-invocation-policy-unit-tests.txt` fails on missing process tools in policy/capability registries; process evidence transcripts fail on missing receipts or wrong lineage.
- Passing test and transcript: `bundle://proof/SB09/transcripts/maf-hidden-dependency-scan.txt`, `bundle://proof/SB09/transcripts/maf-static-dependency-guard-test.txt`, `bundle://proof/SB09/transcripts/agent-runtime-tool-provider-unit-tests.txt`, `bundle://proof/SB09/transcripts/agent-tool-invocation-policy-unit-tests.txt`, `bundle://proof/SB09/transcripts/process-runtime-provider-integration-tests.txt`, `bundle://proof/SB09/transcripts/agent-framework-execution-capability-filtering-tests.txt`, `bundle://proof/SB09/transcripts/process-outbox-tests.txt`, `bundle://proof/SB09/transcripts/process-receipt-semantics-tests.txt`, `bundle://proof/SB09/transcripts/process-artifact-lineage-tests.txt`, and `bundle://proof/SB09/transcripts/final-solution-build.txt` pass.
- Changed source files and hashes: `bundle://proof/SB09/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB09/source-assertions/final-proof-audit.txt` and `bundle://reviews/02-final-red-team-review.md`.
- Red-team negative case: Reintroducing a MAF direct Processes reference, old builder name, missing process tool, missing provider registration, weakened policy entry, missing tool receipt, or wrong artifact lineage would fail the final test/scan set.
- Browser validation: N/A. The bundle changed runtime composition, tests, and documentation; no rendered UI route was changed or exercised.
- Downstream dependency check: Next process contracts/core extraction bundle may start only with the SB09 entry-smoke set repeated.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB09 adds closure proof and documentation only; it introduces no persisted production signal, state, record, or event. | N/A | N/A | N/A |
