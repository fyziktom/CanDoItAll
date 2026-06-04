# SB11 Proof Manifest

- Subbundle: `SB11`
- Status: `Completed`
- Owned requirements: `RQ-012`
- Raw notes: Integration smoke must prove final provider composition, process evidence semantics, subprocess artifact lineage, and one real process runtime path.
- Semantic invariant contract: `bundle://proof/SB11/semantic-invariants.md`

## Changed File Hashes

- Hash manifest: `bundle://proof/SB11/source-assertions/changed-file-hashes.txt`
- Representative hashes:
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` ED3C4F13173213AC267FC91C911A62F86641881B9185B925035101371D9F1701
- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.GovernedRules.cs` B89FABDB707207E301D2A662FC955BD7B1F991F0614D498D2BAC9727543DBFE9
- `tests/CanDoItAll.Tests.Integration/ProcessSubprocessIntegrationTests.cs` F5444CA6164F55B2B68EBC11109C02884FFC7480C62980AEA1D179546EF1DA26

## Command Transcripts

- Unit test suite: `bundle://proof/SB11/transcripts/dotnet-test-unit-full.txt`
- Process-filtered integration suite: `bundle://proof/SB11/transcripts/dotnet-test-integration-process.txt`
- Solution build: `bundle://proof/SB11/transcripts/dotnet-build-slnx.txt`
- Whitespace check: `bundle://proof/SB11/transcripts/git-diff-check.txt`
- Anti-stub audit: `bundle://proof/SB11/transcripts/anti-stub-audit.txt`
- Adversarial old subprocess projection scan: `bundle://proof/SB11/transcripts/adversarial-old-subprocess-projection-scan.txt`

## Failing-First And Passing Proof

- Adversarial negative proof: `bundle://proof/SB11/transcripts/adversarial-old-subprocess-projection-scan.txt` records a non-zero old-behavior scan for child-path reuse in parent subprocess projection.
- Passing: `bundle://proof/SB11/transcripts/dotnet-test-unit-full.txt`, `bundle://proof/SB11/transcripts/dotnet-test-integration-process.txt`, `bundle://proof/SB11/transcripts/dotnet-build-slnx.txt`, and `bundle://proof/SB11/transcripts/git-diff-check.txt`.

## Source Assertions

- Source assertions: `bundle://proof/SB11/source-assertions/integration-smoke-source-assertions.txt`
- Changed-file hashes: `bundle://proof/SB11/source-assertions/changed-file-hashes.txt`

## Anti-Stub Audit

- Anti-stub audit transcript: `bundle://proof/SB11/transcripts/anti-stub-audit.txt`

## Browser And Host Proof

- Browser proof: N/A; SB11 changed process runtime source/tests only and no rendered UI route changed.
- Host proof: N/A; no desktop or long-running host behavior changed.

## Downstream Smoke Proof

- `bundle://proof/SB11/transcripts/dotnet-test-unit-full.txt` proves zero-provider startup, policy, static guard, docs parity, and unit-level process/runtime invariants still pass.
- `bundle://proof/SB11/transcripts/dotnet-test-integration-process.txt` proves provider composition, process outbox/receipt semantics, subprocess artifact lineage, and real process runtime paths pass under the process-filtered integration suite.
- `bundle://proof/SB11/transcripts/dotnet-build-slnx.txt` proves the full solution builds with zero warnings and zero errors.
