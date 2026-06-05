# SB12 Semantic Invariants

- Invariant ID: SB12-INV-001
- Source raw note: Preserve direct-agent candidate defaults, binding facts, recovery execution id, and manual recovery directive behavior.
- Expected behavior: Direct-agent candidates require resolved direct-agent facts and preserve technical agent id, optional chat session id, recovery execution id, manual directive, common context fields, and cooperation metadata.
- Disallowed shallow implementation: A direct-agent factory that silently substitutes empty ids, loses recovery facts, or accepts missing direct-agent facts.
- Failing-first test: N/A process/non-production exemption: SB12 reused the SB04/SB08 failing-first boundary proof and added an adversarial missing-facts passing test because the direct-agent move was completed in the same extraction pass.
- Passing test: bundle://proof/SB08/transcripts/sb08-integration-route-parity-tests.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateAssemblyContext.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- Production assertions: Direct-agent facts are resolved before factory construction and missing facts throw InvalidOperationException.
- Red-team negative case: ProcessDispatchCandidateFactory_CreateDirectAgentCandidate_requires_resolved_direct_agent_facts rejects a shallow defaulting implementation.
- Downstream dependency check: SB16 build and source scans confirm side effects stayed outside factory helpers.

- Invariant ID: SB12-INV-002
- Source raw note: Keep technical-agent binding/access mutation explicit and testable.
- Expected behavior: SaveAgentAsync remains in ProcessDispatchTechnicalAgentBindingCoordinator, and recovery queries remain in ProcessDispatchRecoveryQueryHelper/dispatcher wrappers.
- Disallowed shallow implementation: Hiding SaveAgentAsync or recovery lookup inside the candidate factory.
- Failing-first test: N/A process/non-production exemption: this gate is a side-effect boundary assertion backed by source scans and existing positive tests.
- Passing test: bundle://proof/SB10/transcripts/sb10-project-structure-access-tests.txt; bundle://proof/SB11/transcripts/sb11-recovery-intent-tests.txt; bundle://proof/SB12/transcripts/sb12-binding-recovery-architecture-test.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs
- Production assertions: Binding/access mutation and recovery directive loading remain visible before ProcessDispatchCandidateFactory.CreateDirectAgentCandidate.
- Red-team negative case: SB16 side-effect scan rejects SaveAgentAsync, executionClient, and technicalAgentBridge tokens in factory/context helpers.
- Downstream dependency check: SB17 final red-team repeats the no-hidden-side-effects scan.
