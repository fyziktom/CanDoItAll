# SB17 Proof Manifest

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
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateAssemblyContext.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCooperationMetadataResolver.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Semantic Contract
- bundle://proof/SB17/semantic-invariants.md

## Command Transcripts
- Source assertions: bundle://proof/SB17/transcripts/sb17-source-assertions.txt
- Failing-first transcript: N/A process/non-production exemption; SB17 is final verifier proof over earlier failing-first and passing gate artifacts.
- Passing transcript: bundle://proof/SB17/transcripts/sb17-final-red-team-scans.txt
- Passing transcript: bundle://proof/SB16/transcripts/sb16-full-solution-build.txt
- Anti-stub audit transcript: bundle://proof/SB17/transcripts/sb17-final-red-team-scans.txt

## Test Names
- Test name: Process_dispatch_candidate_factory_gate_a_SB04_INV_001_uses_module_local_side_effect_free_factory
- Test name: Process_dispatch_candidate_factory_gate_b_SB08_INV_001_owns_all_dispatch_candidate_construction
- Test name: Process_dispatch_cooperation_resolver_SB13_INV_001_moves_profile_resolution_without_driver_api

## Semantic Proof
- Invariant ID: SB17-INV-001
- Shallow-pass trap: Final closure could claim success while hiding missing manifests, stubs, driver drift, or prohibited proof artifacts.
- Adversarial negative proof: N/A process/non-production exemption; final red-team scans are cited under semantic positive proof.
- Semantic positive proof: bundle://proof/SB17/transcripts/sb17-final-red-team-scans.txt and bundle://proof/SB16/transcripts/sb16-full-solution-build.txt
- Anti-stub audit: bundle://proof/SB17/transcripts/sb17-final-red-team-scans.txt reports no helper stubs.

