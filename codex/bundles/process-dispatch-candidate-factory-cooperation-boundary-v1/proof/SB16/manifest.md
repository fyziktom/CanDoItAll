# SB16 Proof Manifest

## Changed File Hashes
- repo://src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessDispatchCandidateAssemblyContext.cs SHA-256: 0e92e04f21dae782d3a5d22a8817b84c5269936fb927cefc7aa032988cfdaf88
- repo://src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessDispatchCandidateFactory.cs SHA-256: d76d58ff1cea5fe3c6f464d753ac98e9e7a5e3f99f85cc3643303151882cf8e6
- repo://src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessDispatchCooperationMetadataResolver.cs SHA-256: 09f8bbdc6b2e375c2bb3ada0ab0d79ffc1870ffb665c4941ef03177c4b8ebc70
- repo://src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Dispatch.cs SHA-256: ac2bcf94fdb9fb7320b054bd430f0578dac622116536b09f70aeb2102b8dd97d
- repo://src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessRunAutomationDispatchService.Cooperation.cs SHA-256: 019f2678973e3106da73934bd7ebb16f789d5f6b936d3c87e5c88d89cfa1bd17
- repo://tests\CanDoItAll.Tests.Unit\ProcessAgentExecutionBoundaryArchitectureTests.cs SHA-256: 4c6181e8f1088b8a56966a8bad5abd209a4174007aeb5e129ad343d50e3fdfbb
- repo://tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs SHA-256: 5435e8db1c6df00afde15cfdb5e703fee92df000b7bb846e079be13be89ee190
- repo://codex\bundles\process-dispatch-candidate-factory-cooperation-boundary-v1\inventories\02-candidate-field-map-template.md SHA-256: 5f5408ab965994ecaf407035e90da443aacadb555ba022d57704d945aa09e9d4
- repo://codex\bundles\process-dispatch-candidate-factory-cooperation-boundary-v1\inventories\03-driver-readiness-candidate-map-template.md SHA-256: a28e93f81ff3be09ca2146a2774b44d352ccf7177f9c3e09d8e6ba56bd195258

## Portable Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Cooperation.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs

## Semantic Contract
- bundle://proof/SB16/semantic-invariants.md

## Command Transcripts
- Source assertions: bundle://proof/SB16/transcripts/sb16-source-assertions.txt
- Failing-first transcript: N/A process/non-production exemption; SB16 is a smoke/proof gate over already-tested movement.
- Passing transcript: bundle://proof/SB16/transcripts/sb16-full-solution-build.txt
- Passing transcript: bundle://proof/SB16/transcripts/sb16-line-counts-and-source-scans.txt
- Anti-stub audit transcript: bundle://proof/SB16/transcripts/sb16-line-counts-and-source-scans.txt

## Semantic Proof
- Invariant ID: SB16-INV-001
- Shallow-pass trap: Build-only proof would miss constructor ownership drift, hidden side effects, helper stubs, or line-count regression.
- Adversarial negative proof: N/A process/non-production exemption; source scans are cited under semantic positive proof.
- Semantic positive proof: bundle://proof/SB16/transcripts/sb16-full-solution-build.txt reports ExitCode: 0.
- Anti-stub audit: bundle://proof/SB16/transcripts/sb16-line-counts-and-source-scans.txt reports no TODO or NotImplemented markers in new helpers.

