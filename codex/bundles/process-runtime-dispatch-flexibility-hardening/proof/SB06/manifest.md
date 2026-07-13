# SB06 Proof Manifest

## Closure

- Status: Completed
- Scope: backend/runtime architecture refactor; no browser-visible UI files changed.
- Semantic invariant contract: bundle://proof/SB06/semantic-invariants.md
- Passing transcript: bundle://proof/SB07/transcripts/build-modules-processes.txt
- Passing transcript: bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt
- Passing transcript: bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt
- Anti-stub audit transcript: bundle://proof/SB07/transcripts/architecture-audits.txt
- Failing-first: N/A - process refactor preserves production behavior; no production behavior was removed, and the adversarial negative proof is enforced by boundary tests plus dependency scans.

## Source Assertions

- StandardProcessAdapterStrategyFactory consumes IProcessStepExecutionDriver and ProcessRuntimeDispatchApplicationService consumes IProcessRuntimeBranchSignalRouter.
- The old ProcessRuntimeIntegrationServices.cs mega-file is deleted and runtime integration now lives in focused files under src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration.
- Generic src/Processes projects expose typed ports and do not reference MAF, AgentFramework implementation projects, or module wrapper assemblies.
- Prompt composition, step execution dispatch, completion evidence, branch signaling, recovery, telemetry, and launch resolution remain behavior-preserving refactors verified by the shared transcripts.

## Changed File Hashes

- repo://src/Processes/CanDoItAll.Processes.Application/ProcessPromptCompositionContracts.cs sha256:eb43c71e109ea3278d88d636c6d570516987c48c84a28a9ee919885d993ddc13
- repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverExecutionContracts.cs sha256:a8a743d3a12938e59fd024c9d5f807cf6c530cc88e127d10451aadbe9d15cf36
- repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterStrategyFactory.cs sha256:f314c6b0cf99442afec560f5c5c6f5f119e0a309a62814c3b3bbcc729e2c3867
- repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDriverPackageFactory.cs sha256:31e19257044591cb0b126499218c8733dcab03e3ee41c0bec6d26264d3e15ec7
- repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeBranchSignalApplicationService.cs sha256:f66056de42588ec12d2c0689175f28c2b926ffa11018f4e8a8670a9b20a60048
- repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs sha256:7798212f52578d04c26c2ee8099badd4601b2215d7fb1e546ed2133058a01ab4
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs sha256:1e02ebe9e00beb924580d772694045c08dae9c2e43c75fa59542728483e6e8ea
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs sha256:23b502661471e7c63165ece5e5b4074047ed11d692583991bec419ed04bd0f71
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs sha256:9d343426c376a3792a0fb9cc8adcb97044b76305c6b79fb2bde9cb61a3e75f38
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs sha256:64fac15acc785b18a221363775ddfd39dce0fd28431604c011dcb1ee9d32bb74
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs sha256:b09bbdc6c8b80b201900f428ff7fadbd045e0e673ad1cfb0c673ca37d382e74d
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs sha256:ffa5e746d372d35f936569a367281e9758c9de60590ad91d2e5ff5c80a8cdb43
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessLaunchExecutorResolver.cs sha256:f513ad80f7e22b19c9576734e5dcc9b2de2c0fc08953be8febbf962e302bfd78
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/StandardProcessRuntimeStrategyFactoryResolver.cs sha256:f638cd82a39b6fd2724f61e010ab26fcd9c0b3443c3e2a5201aabac1f393a265
- repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/StandardProcessLaunchDriverCatalogProvider.cs sha256:e4c1d87110fba9e07a222ab02e39aeba3a8c6e69aef5dc05e31537bc6999c407
- repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessExecutionAdapterBoundaryTests.cs sha256:5624cb3e6ae611a2b4613811550dd052febf910a99d41662c0dd91ab8d9f0a53
- repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs sha256:0e5f65720970e1b79f43e81821d2a8b9a16992f02954dbc90aa42f9f1c42ba41

## Command Evidence

- Command transcript: bundle://proof/SB07/transcripts/build-modules-processes.txt
- Command transcript: bundle://proof/SB07/transcripts/unit-focused-process-runtime.txt
- Command transcript: bundle://proof/SB07/transcripts/integration-e2e-process-runtime.txt
- Command transcript: bundle://proof/SB07/transcripts/architecture-audits.txt

## Anti-Stub Audit

- Anti-stub audit transcript: bundle://proof/SB07/transcripts/architecture-audits.txt
- Result: no TODO, NotImplemented, fixture-specific branch, hardcoded placeholder, or stub was found in changed process runtime/driver files. The only match is the production diagnostic phrase 	emporary failure.
