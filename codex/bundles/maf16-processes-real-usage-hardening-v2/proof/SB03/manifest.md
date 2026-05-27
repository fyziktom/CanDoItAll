# SB03 Proof Manifest

## Scope

- Subbundle: SB03 - Message injection and finalizer structured output.
- Invariant ID: SB03-INV-001
- Shipped behavior: Context injection and finalizer output stay explicit, typed, and validated without unsupported package symbols.

## Source Proof

- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentContextContributionProvider.cs
- repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs
- bundle://proof/SB03/semantic-invariants.md
- bundle://analysis/04-maf16-feature-adoption-matrix.md

## Command Transcripts

- Passing transcript: bundle://proof/SB03/transcripts/passing.txt
- Adversarial negative proof transcript: bundle://proof/SB03/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/SB03/transcripts/anti-stub-audit.txt
- Source assertions transcript: bundle://proof/SB03/transcripts/source-assertions.txt
- Changed-file hashes transcript: bundle://proof/SB03/transcripts/changed-file-hashes.txt

## Changed File Hashes

- repo://codex/bundles/maf16-processes-real-usage-hardening-v2/analysis/04-maf16-feature-adoption-matrix.md: 19B3DD358326D819E0D890A76F8111A622DF45D513D05EF0118F221ED946DBDB
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs: EE2154A3C026E749BED344F798887FB5B1633CD644751BF4DFE25901E1D931FD
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs: 61ADE5D9098CB0549F2AAD53A8CC381B88D0785A0263CDE8EBDCBE418BA2CC29
