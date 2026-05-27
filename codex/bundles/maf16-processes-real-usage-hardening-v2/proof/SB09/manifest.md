# SB09 Proof Manifest

## Scope

- Subbundle: SB09 - Adapter boundary checkpoint.
- Invariant ID: SB09-INV-001
- Shipped behavior: Adapter boundaries keep MAF-specific dependencies out of Processes and shared contracts.

## Source Proof

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs
- repo://src/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs
- repo://tests/CanDoItAll.Tests.Unit/WorkflowArchitectureBoundaryTests.cs
- bundle://proof/SB09/semantic-invariants.md
- bundle://analysis/04-maf16-feature-adoption-matrix.md

## Command Transcripts

- Passing transcript: bundle://proof/SB09/transcripts/passing.txt
- Adversarial negative proof transcript: bundle://proof/SB09/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/SB09/transcripts/anti-stub-audit.txt
- Source assertions transcript: bundle://proof/SB09/transcripts/source-assertions.txt
- Changed-file hashes transcript: bundle://proof/SB09/transcripts/changed-file-hashes.txt

## Changed File Hashes

- repo://codex/bundles/maf16-processes-real-usage-hardening-v2/analysis/04-maf16-feature-adoption-matrix.md: 19B3DD358326D819E0D890A76F8111A622DF45D513D05EF0118F221ED946DBDB
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs: EE2154A3C026E749BED344F798887FB5B1633CD644751BF4DFE25901E1D931FD
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs: 61ADE5D9098CB0549F2AAD53A8CC381B88D0785A0263CDE8EBDCBE418BA2CC29

