# SB04 Proof Manifest

- Subbundle: `SB04`
- Status: `Completed`
- Owned requirements: `RQ-005`
- Raw notes: Project-structure runtime tools must move out of MAF into the owning module provider without tool-name or policy drift.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`

## Changed File Hashes

- Representative SHA-256: ba73edfa4165b02f7e948eefca7dd72e16faafade145383ea045af88ae2a5e3c  repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs
- Hash manifest: `bundle://proof/SB04/source-assertions/changed-file-hashes.txt`

## Command Transcripts

- Project-structure tool builder before/after inventory: `bundle://proof/SB04/transcripts/project-structure-tool-builder-inventory.txt`
- MAF dependency scan and project-reference decision: `bundle://proof/SB04/transcripts/maf-project-structure-dependency-scan.txt`
- Failing-first runtime-provider DI validation: `bundle://proof/SB04/transcripts/failing-first-runtime-provider-di-validation.txt`
- ProjectStructure unit tests: `bundle://proof/SB04/transcripts/project-structure-unit-tests.txt`
- Runtime tool provider composition integration tests: `bundle://proof/SB04/transcripts/runtime-tool-provider-composition-integration-tests.txt`
- Solution build: `bundle://proof/SB04/transcripts/solution-build.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Failing-First And Passing Proof

- Failing-first: `bundle://proof/SB04/transcripts/failing-first-runtime-provider-di-validation.txt` records the initial integration-test failure caused by depending on unregistered `IWorkspaceCommandExecutionService`; the provider now constructs the same default command service used by the former MAF fallback.
- Passing: `bundle://proof/SB04/transcripts/project-structure-unit-tests.txt`, `bundle://proof/SB04/transcripts/runtime-tool-provider-composition-integration-tests.txt`, and `bundle://proof/SB04/transcripts/solution-build.txt`.

## Source Assertions

- Source assertions: `bundle://proof/SB04/source-assertions/project-structure-provider-source-assertions.txt`
- Changed-file hashes: `bundle://proof/SB04/source-assertions/changed-file-hashes.txt`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Browser And Host Proof

- Browser proof: N/A; SB04 changes runtime provider composition and source ownership, not rendered UI routes.
- Host proof: N/A; no desktop process-launch behavior changed beyond preserving the existing Git status helper path inside the provider.

## Downstream Smoke Proof

- `bundle://proof/SB04/transcripts/runtime-tool-provider-composition-integration-tests.txt` proves Workbench and Processes providers coexist in app composition.
- `bundle://proof/SB04/transcripts/solution-build.txt` proves the MAF direct Projects reference removal still compiles.
