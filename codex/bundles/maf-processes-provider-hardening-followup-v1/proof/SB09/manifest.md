# SB09 Proof Manifest

- Subbundle: `SB09`
- Status: `Completed`
- Owned requirements: `RQ-010`
- Raw notes: Runtime tool-provider ownership must be observable in attach diagnostics and receipt evidence before driver-pack work.
- Semantic invariant contract: `bundle://proof/SB09/semantic-invariants.md`

## Changed File Hashes

- Hash manifest: `bundle://proof/SB09/source-assertions/changed-file-hashes.txt`

## Command Transcripts

- Runtime tool provider composition tests: `bundle://proof/SB09/transcripts/dotnet-test-unit-maf-runtime-tool-provider-composition.txt`
- Tool receipt semantics tests: `bundle://proof/SB09/transcripts/dotnet-test-unit-workspace-file-service-receipts.txt`
- Process runtime provider composition smoke: `bundle://proof/SB09/transcripts/dotnet-test-integration-process-runtime-provider.txt`
- Process provider access smoke: `bundle://proof/SB09/transcripts/dotnet-test-integration-process-agent-runtime-tool-provider-access.txt`
- Required receipt integration gate: `bundle://proof/SB09/transcripts/dotnet-test-integration-receipt.txt`
- Solution build: `bundle://proof/SB09/transcripts/dotnet-build-slnx.txt`

## Failing-First And Passing Proof

- Failing-first: New unit assertions fail if provider attach diagnostics omit provider key/display name, if receipts do not preserve runtime provider ownership, or if legacy receipt JSON stops deserializing with empty provider ownership.
- Failing-first: Existing process receipt tests fail when project-structure writeback claims are accepted without required `project_structure_*` receipts.
- Passing: `bundle://proof/SB09/transcripts/dotnet-test-unit-maf-runtime-tool-provider-composition.txt`, `bundle://proof/SB09/transcripts/dotnet-test-unit-workspace-file-service-receipts.txt`, `bundle://proof/SB09/transcripts/dotnet-test-integration-receipt.txt`, and `bundle://proof/SB09/transcripts/dotnet-build-slnx.txt`.

## Source Assertions

- Runtime provider observability assertions: `bundle://proof/SB09/source-assertions/runtime-provider-observability.txt`
- Process receipt required-tool guard assertions: `bundle://proof/SB09/source-assertions/process-receipt-required-tool-guards.txt`
- Changed-file hashes: `bundle://proof/SB09/source-assertions/changed-file-hashes.txt`

## Anti-Stub Audit

- Anti-stub scan: `bundle://proof/SB09/source-assertions/anti-stub-scan.txt`

## Browser And Host Proof

- Browser proof: N/A; SB09 changed runtime/provider diagnostics, receipt projection, tests, and docs only.
- Host proof: N/A; no desktop or long-running process-launch behavior changed.

## Downstream Smoke Proof

- `bundle://proof/SB09/transcripts/dotnet-test-integration-process-runtime-provider.txt` proves process runtime-provider composition still passes.
- `bundle://proof/SB09/transcripts/dotnet-test-integration-process-agent-runtime-tool-provider-access.txt` proves provider access behavior still passes after receipt observability changes.
- `bundle://proof/SB09/transcripts/dotnet-test-integration-receipt.txt` proves receipt schema compatibility and process receipt semantics pass.
