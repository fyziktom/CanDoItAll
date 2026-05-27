# SB12 Proof Manifest

## Scope

- Subbundle: SB12 - Satisfaction read model and finalizer parity.
- Invariant ID: SB12-INV-001
- Shipped behavior: Read model satisfaction and finalizer validation remain aligned under the same artifact validity rules.

## Source Proof

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- bundle://proof/SB12/semantic-invariants.md
- bundle://analysis/04-maf16-feature-adoption-matrix.md

## Command Transcripts

- Passing transcript: bundle://proof/SB12/transcripts/passing.txt
- Adversarial negative proof transcript: bundle://proof/SB12/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/SB12/transcripts/anti-stub-audit.txt
- Source assertions transcript: bundle://proof/SB12/transcripts/source-assertions.txt
- Changed-file hashes transcript: bundle://proof/SB12/transcripts/changed-file-hashes.txt

## Changed File Hashes

- repo://codex/bundles/maf16-processes-real-usage-hardening-v2/analysis/04-maf16-feature-adoption-matrix.md: 19B3DD358326D819E0D890A76F8111A622DF45D513D05EF0118F221ED946DBDB
- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs: EE2154A3C026E749BED344F798887FB5B1633CD644751BF4DFE25901E1D931FD
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs: 61ADE5D9098CB0549F2AAD53A8CC381B88D0785A0263CDE8EBDCBE418BA2CC29
