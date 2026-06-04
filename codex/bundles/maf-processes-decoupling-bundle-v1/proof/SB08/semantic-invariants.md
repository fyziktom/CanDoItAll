# SB08 Semantic Invariants

## Invariant `SB08-INV-001`

- Invariant ID: SB08-INV-001
- Source raw note: MAF and Processes must be decoupled without simplifying or omitting behavior, and operators must not be left with stale guidance.
- Expected behavior: Live documentation describes the provider seam, does not point operators at the deleted MAF process-tool partial, and clearly bounds process-core extraction as future work.
- Disallowed shallow implementation: Only update one README, leave stale process-builder references in live docs, or claim that process-core/driver extraction is complete.
- Failing-first test and transcript: Live stale-reference scan at `bundle://proof/SB08/transcripts/stale-reference-scan-live-docs.txt` fails if live README/docs/src content reintroduces deleted process-builder names.
- Passing test and transcript: `bundle://proof/SB08/transcripts/stale-reference-scan-live-docs.txt`, `bundle://proof/SB08/transcripts/git-diff-check.txt`, and `bundle://proof/SB08/transcripts/solution-build.txt` pass.
- Changed source files and hashes: `bundle://proof/SB08/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB08/source-assertions/documentation-source-assertion.txt` and `bundle://proof/SB08/source-assertions/historical-reference-classification.txt`.
- Red-team negative case: A stale `ProcessToolBuilder` or `MafAgentRuntime.ProcessTools` reference in live documentation would fail the live-doc scan; a historical bundle-only match is classified separately.
- Browser validation: N/A. Documentation-only change with no rendered UI route exercised.
- Downstream dependency check: SB09 can start because operator documentation now matches the provider-based runtime seam.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB08 changes documentation only; it introduces no persisted production signal, state, record, or event. | N/A | N/A | N/A |
