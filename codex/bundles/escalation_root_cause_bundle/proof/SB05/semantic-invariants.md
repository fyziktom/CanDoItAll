# SB05 Semantic Invariants

## Lifecycle Separation

- Structured finalizer validity is not semantic process completion.
- A valid finalizer output with a produced artifact slot creates a staged managed artifact lifecycle state, not an accepted output.
- Completion gate failure returns `NeedsManager` diagnostics with no `ProducedArtifacts`, even when the staged managed artifact file exists for debugging.
- Completion gate success is the only path that appends `Runtime Accepted Completion Gates`.
- Produced artifact content hashes are computed after gate acceptance, so promoted slots reflect accepted artifact content.

## Runtime Wording

- Pre-gate text uses `Runtime Captured Structured Outcome` and states that completion gates have not accepted the output yet.
- The removed wording `Runtime Validated Structured Outcome` no longer appears in source or unit tests.
- The recovered completed-artifact path says the runtime staged the primary artifact, not that it accepted it before gates.

## Parent And Consumer Visibility

- `ToAdapterResult` still emits `ProducedArtifacts` only for `Succeeded` outcomes after completion gate evaluation.
- `ParentSubprocessArtifactBridge` now reads typed child artifact candidates and refuses a file that has the captured marker without `Runtime Accepted Completion Gates`.
- Legacy typed child output files that do not carry the new captured marker retain current behavior, but newly staged runtime-owned files cannot bridge without acceptance proof.

## Artifact Acceptance Matrix

| State | File exists | Captured marker | Accepted marker | Produced slot | Parent bridge |
| --- | --- | --- | --- | --- | --- |
| Invalid finalizer | No runtime materialization | No | No | No | No |
| Staged, gates rejected | Yes | Yes | No | No | No |
| Staged, gates accepted | Yes | Yes | Yes | Yes | Yes |
| Legacy accepted child file | Yes | No | No | Runtime-state dependent | Preserved |

## Architecture

- MAF remains responsible for finalizer schema validity.
- Process runtime adapter owns lifecycle staging, gate acceptance, and slot promotion.
- The explicit lifecycle record is internal to the adapter and does not leak process acceptance semantics into MAF.
- CodeAnalytics snapshot `snap-20260708191340-60b7e58e` reported no scoped dependency cycles.


## Completed Validator Contract

- Invariant ID: SB05-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB05/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB05/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/05-sb05-managed-artifact-acceptance-order/README.md and bundle://proof/SB05/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.


## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB05 semantic proof metadata | proof/SB05/semantic-invariants.md | proof/SB05/transcripts/00-validator-metadata.txt | final proof closure | proof/SB05/manifest.md rejects missing semantic proof |
