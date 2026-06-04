# SB05 Proof Manifest

- Subbundle: `SB05`
- Status: `Completed`
- Owned requirements: `RQ-006`
- Raw notes: Image-generation runtime tools must move out of MAF into the runtime-provider seam while preserving eligible-agent availability and approval policy.
- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`

## Changed File Hashes

- Hash manifest: `bundle://proof/SB05/source-assertions/changed-file-hashes.txt`

## Command Transcripts

- Image-generation tool/access inventory: `bundle://proof/SB05/transcripts/image-generation-tool-inventory.txt`
- Required MAF attach-name scan: `bundle://proof/SB05/transcripts/image-generation-maf-attach-scan.txt`
- MAF image dependency scan: `bundle://proof/SB05/transcripts/maf-image-dependency-scan.txt`
- Failing-first MAF helper dependency build: `bundle://proof/SB05/transcripts/failing-first-maf-helper-dependency-build.txt`
- ImageGeneration unit tests: `bundle://proof/SB05/transcripts/image-generation-unit-tests.txt`
- Runtime availability integration smoke: `bundle://proof/SB05/transcripts/image-generation-runtime-integration-smoke.txt`
- Solution build: `bundle://proof/SB05/transcripts/solution-build.txt`
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`

## Failing-First And Passing Proof

- Failing-first: `bundle://proof/SB05/transcripts/failing-first-maf-helper-dependency-build.txt` records the first SB05 build failure after the move because the copied builder still referenced MAF-local helpers (`ProviderFeatureService`, `SerializerOptions`, and `ResolveProviderNetworkTimeout`). Those helpers were made local/explicit in `ImageGenerationAgentRuntimeToolProvider` before passing proof.
- Passing: `bundle://proof/SB05/transcripts/image-generation-unit-tests.txt`, `bundle://proof/SB05/transcripts/image-generation-runtime-integration-smoke.txt`, and `bundle://proof/SB05/transcripts/solution-build.txt`.

## Source Assertions

- Source assertions: `bundle://proof/SB05/source-assertions/image-generation-provider-source-assertions.txt`
- Changed-file hashes: `bundle://proof/SB05/source-assertions/changed-file-hashes.txt`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub-audit.txt`

## Browser And Host Proof

- Browser proof: N/A; SB05 changes runtime provider composition and source ownership, not rendered UI routes.
- Host proof: N/A; no desktop process-launch behavior changed.

## Downstream Smoke Proof

- `bundle://proof/SB05/transcripts/image-generation-runtime-integration-smoke.txt` proves eligible agents still receive `image_generation_create` at runtime.
- `bundle://proof/SB05/transcripts/maf-image-dependency-scan.txt` proves MAF direct Workbench/Projects image coupling was removed.
