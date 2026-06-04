# SB10 Proof Manifest

- Subbundle: `SB10`
- Status: `Completed`
- Owned requirements: `RQ-011`
- Raw notes: Live documentation and static guardrails must describe the providerized first-party tool boundary without claiming process-core extraction.
- Semantic invariant contract: `bundle://proof/SB10/semantic-invariants.md`

## Changed File Hashes

- Hash manifest: `bundle://proof/SB10/source-assertions/changed-file-hashes.txt`

## Command Transcripts

- Stale reference scan: `bundle://proof/SB10/source-assertions/stale-reference-scan.txt`
- Static architecture tests: `bundle://proof/SB10/transcripts/dotnet-test-unit-agent-runtime-tool-provider-architecture.txt`
- Docs and skill parity tests: `bundle://proof/SB10/transcripts/dotnet-test-unit-api-docs-skills-parity.txt`
- Whitespace check: `bundle://proof/SB10/transcripts/git-diff-check.txt`
- Solution build: `bundle://proof/SB10/transcripts/dotnet-build-slnx.txt`

## Failing-First And Passing Proof

- Failing-first: The docs parity test fails if live docs stop naming the process, project-structure, or image-generation runtime providers, omit provider diagnostics, or claim process-core extraction is complete.
- Failing-first: The stale reference scan fails if live source/docs/skills reintroduce removed hard-coded MAF attach method names.
- Passing: `bundle://proof/SB10/transcripts/dotnet-test-unit-agent-runtime-tool-provider-architecture.txt`, `bundle://proof/SB10/transcripts/dotnet-test-unit-api-docs-skills-parity.txt`, and `bundle://proof/SB10/transcripts/dotnet-build-slnx.txt`.

## Source Assertions

- Runtime provider docs assertions: `bundle://proof/SB10/source-assertions/runtime-provider-doc-source-assertions.txt`
- Stale reference scan: `bundle://proof/SB10/source-assertions/stale-reference-scan.txt`
- Changed-file hashes: `bundle://proof/SB10/source-assertions/changed-file-hashes.txt`

## Browser And Host Proof

- Browser proof: N/A; SB10 changed live documentation and static tests only.
- Host proof: N/A; no desktop, browser, or long-running process-launch behavior changed.

## Downstream Smoke Proof

- `bundle://proof/SB10/transcripts/dotnet-test-unit-agent-runtime-tool-provider-architecture.txt` proves the provider boundary guardrails remain enforced.
- `bundle://proof/SB10/transcripts/dotnet-test-unit-api-docs-skills-parity.txt` proves docs and skills describe the current providerized runtime-tool boundary.
