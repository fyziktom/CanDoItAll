# SB12 Proof Manifest

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
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateAssemblyContext.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchTechnicalAgentBindingCoordinator.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Semantic Contract
- bundle://proof/SB12/semantic-invariants.md

## Command Transcripts
- Source assertions: bundle://proof/SB12/transcripts/sb12-source-assertions.txt
- Failing-first transcript: N/A process/non-production exemption; direct-agent move is covered by SB04/SB08 failing-first boundary proof and SB12 adversarial missing-facts positive test.
- Passing transcript: bundle://proof/SB08/transcripts/sb08-integration-route-parity-tests.txt
- Passing transcript: bundle://proof/SB10/transcripts/sb10-project-structure-access-tests.txt
- Passing transcript: bundle://proof/SB11/transcripts/sb11-recovery-intent-tests.txt
- Passing transcript: bundle://proof/SB12/transcripts/sb12-binding-recovery-architecture-test.txt
- Anti-stub audit transcript: bundle://proof/SB16/transcripts/sb16-line-counts-and-source-scans.txt

## Test Names
- Test name: ProcessDispatchCandidateFactory_CreateDirectAgentCandidate_preserves_binding_recovery_and_cooperation_facts
- Test name: ProcessDispatchCandidateFactory_CreateDirectAgentCandidate_requires_resolved_direct_agent_facts
- Test name: Process_dispatch_candidate_hydration_gate_d_SB16_INV_001_keeps_binding_side_effects_explicit_and_recovery_queries_local

## Semantic Proof
- Invariant ID: SB12-INV-001
- Invariant ID: SB12-INV-002
- Shallow-pass trap: A direct-agent helper could silently default missing ids or hide SaveAgentAsync in a pure-looking factory.
- Adversarial negative proof: N/A process/non-production exemption; missing-facts coverage is cited under semantic positive proof.
- Semantic positive proof: bundle://proof/SB08/transcripts/sb08-integration-route-parity-tests.txt and bundle://proof/SB11/transcripts/sb11-recovery-intent-tests.txt
- Anti-stub audit: bundle://proof/SB16/transcripts/sb16-line-counts-and-source-scans.txt reports no TODO or NotImplemented markers in new helpers.

